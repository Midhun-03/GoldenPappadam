import 'dart:convert';

import 'package:drift/drift.dart';
import 'package:drift_flutter/drift_flutter.dart';

part 'database.g.dart';

/// Money and quantities are doubles here on purpose. The phone never decides what anything costs
/// or what a bill comes to - the server recomputes every total from the same rules the admin panel
/// uses - so these are display values and the exact figures live in SQL Server's decimals.
///
/// The cache tables are replaced wholesale on every snapshot. [Outbox] is the only table that is
/// local truth: nothing else on this phone is information the server does not already have.

@DataClassName('CachedCustomer')
class Customers extends Table {
  TextColumn get id => text()();
  TextColumn get name => text()();
  TextColumn get contactPerson => text().nullable()();
  TextColumn get phone => text().nullable()();
  TextColumn get address => text().nullable()();

  /// What the shop owed as at the last sync. Shown with that caveat, never edited here.
  RealColumn get balance => real()();

  /// True for a parent company with several physical shops, e.g. Danya Supermarket. The sale
  /// screen shows a branch picker only when this is set - same rule as the admin panel.
  BoolColumn get hasMultipleBranches => boolean().withDefault(const Constant(false))();

  @override
  Set<Column> get primaryKey => {id};
}

/// One physical shop under a multi-branch customer, e.g. Kundara under Danya Supermarket. Only
/// populated for customers where [Customers.hasMultipleBranches] is true.
@DataClassName('CachedBranch')
class Branches extends Table {
  TextColumn get id => text()();
  TextColumn get customerId => text()();
  TextColumn get name => text()();
  TextColumn get location => text().nullable()();
  TextColumn get address => text().nullable()();
  TextColumn get phone => text().nullable()();
  TextColumn get contactPerson => text().nullable()();

  @override
  Set<Column> get primaryKey => {id};
}

@DataClassName('CachedProduct')
class Products extends Table {
  TextColumn get id => text()();
  TextColumn get productCode => text()();
  TextColumn get name => text()();
  TextColumn get unitCode => text()();
  RealColumn get defaultPrice => real().nullable()();

  @override
  Set<Column> get primaryKey => {id};
}

/// What one shop pays for one product. Read-only on the phone: only the office can change a price,
/// and there is no screen here that offers to.
@DataClassName('CachedPrice')
class CustomerPrices extends Table {
  TextColumn get customerId => text()();
  TextColumn get productId => text()();
  RealColumn get unitPrice => real()();

  @override
  Set<Column> get primaryKey => {customerId, productId};
}

/// Money already received from a shop, as the office has it. Replaced on every snapshot like the
/// rest of the cache: payments the salesperson has just taken live in the outbox until they land.
@DataClassName('CachedPayment')
class Payments extends Table {
  TextColumn get id => text()();
  TextColumn get customerId => text()();

  /// The business date, "2026-09-16".
  TextColumn get paymentDate => text()();

  /// When the office recorded it, for the time of day.
  DateTimeColumn get recordedAt => dateTime()();

  RealColumn get amount => real()();
  TextColumn get method => text()();
  TextColumn get reference => text().nullable()();
  TextColumn get notes => text().nullable()();

  @override
  Set<Column> get primaryKey => {id};
}

/// Anything the app needs to remember between runs: the last sync, the device id, who is signed in.
class Meta extends Table {
  TextColumn get key => text()();
  TextColumn get value => text()();

  @override
  Set<Column> get primaryKey => {key};
}

/// Everything the salesperson has done that the server has not confirmed.
///
/// A row is written before the screen says "saved", and nothing deletes it on failure. The
/// [clientRequestId] is generated once, here, and never regenerated - that single fact is what
/// makes a retry safe, because the server recognises it and returns the original bill instead of
/// making a second one.
class OutboxEntries extends Table {
  TextColumn get clientRequestId => text()();

  /// Invoice, Payment or Visit, matching the server's SubmissionType.
  TextColumn get type => text()();

  /// The request body, exactly as it will be sent. Frozen at the moment of saving, so a later
  /// price change or edit cannot alter what the shop was actually told.
  TextColumn get payload => text()();

  /// UTC, when the salesperson saved it - which may be hours before the server hears about it.
  DateTimeColumn get recordedAt => dateTime()();

  /// Pending, Syncing, Synced or Failed.
  TextColumn get status => text().withDefault(const Constant('Pending'))();

  IntColumn get attemptCount => integer().withDefault(const Constant(0))();

  DateTimeColumn get nextAttemptAt => dateTime().nullable()();

  /// Why the server refused it, in words the salesperson can act on.
  TextColumn get lastError => text().nullable()();

  TextColumn get serverRecordId => text().nullable()();

  /// A line to show in the list: "Kumar Stores - 450".
  TextColumn get summary => text()();

  @override
  Set<Column> get primaryKey => {clientRequestId};
}

enum OutboxStatus { pending, syncing, synced, failed }

extension OutboxStatusName on OutboxStatus {
  String get stored => switch (this) {
        OutboxStatus.pending => 'Pending',
        OutboxStatus.syncing => 'Syncing',
        OutboxStatus.synced => 'Synced',
        OutboxStatus.failed => 'Failed',
      };
}

@DriftDatabase(tables: [Customers, Products, CustomerPrices, Payments, Branches, Meta, OutboxEntries])
class AppDatabase extends _$AppDatabase {
  AppDatabase([QueryExecutor? executor])
      : super(executor ?? driftDatabase(name: 'golden_pappadam'));

  @override
  int get schemaVersion => 3;

  @override
  MigrationStrategy get migration => MigrationStrategy(
        onCreate: (m) => m.createAll(),
        onUpgrade: (m, from, to) async {
          // 2: payment history, so the shop page can answer "when did I last collect from you?"
          // with no signal. A cache table, so there is nothing to carry across - the next snapshot
          // fills it.
          if (from < 2) await m.createTable(payments);

          // 3: branches for multi-branch customers, so the sale screen can ask which shop of
          // Danya Supermarket a bill is for. Both are cache tables/columns - the next snapshot
          // fills them.
          if (from < 3) {
            await m.createTable(branches);
            await m.addColumn(customers, customers.hasMultipleBranches);
          }
        },
      );

  // ---------- the cache ----------

  /// Replaces the whole cache in one transaction, so the app never reads half a snapshot.
  Future<void> replaceSnapshot({
    required List<CustomersCompanion> customers,
    required List<ProductsCompanion> products,
    required List<CustomerPricesCompanion> prices,
    List<PaymentsCompanion> payments = const [],
    List<BranchesCompanion> branches = const [],
  }) =>
      transaction(() async {
        await delete(this.customers).go();
        await delete(this.products).go();
        await delete(customerPrices).go();
        await delete(this.payments).go();
        await delete(this.branches).go();

        await batch((batch) {
          batch.insertAll(this.customers, customers);
          batch.insertAll(this.products, products);
          batch.insertAll(customerPrices, prices);
          batch.insertAll(this.payments, payments);
          batch.insertAll(this.branches, branches);
        });
      });

  /// What the office has received from this shop, newest first.
  Future<List<CachedPayment>> paymentsFor(String customerId) =>
      (select(payments)
            ..where((p) => p.customerId.equals(customerId))
            ..orderBy([(p) => OrderingTerm(expression: p.recordedAt, mode: OrderingMode.desc)]))
          .get();

  Future<List<CachedCustomer>> allCustomers() =>
      (select(customers)..orderBy([(c) => OrderingTerm(expression: c.name)])).get();

  Future<CachedCustomer?> findCustomer(String id) =>
      (select(customers)..where((c) => c.id.equals(id))).getSingleOrNull();

  /// The branches under one multi-branch customer. Empty for a plain shop.
  Future<List<CachedBranch>> branchesFor(String customerId) => (select(branches)
        ..where((b) => b.customerId.equals(customerId))
        ..orderBy([(b) => OrderingTerm(expression: b.name)]))
      .get();

  Future<List<CachedProduct>> allProducts() =>
      (select(products)..orderBy([(p) => OrderingTerm(expression: p.name)])).get();

  /// The prices that apply to one shop: the agreed price where there is one, the product's own
  /// price otherwise. Exactly the rule the server uses when it builds the bill.
  Future<Map<String, double?>> pricesFor(String customerId) async {
    final agreed = await (select(customerPrices)
          ..where((p) => p.customerId.equals(customerId)))
        .get();

    final byProduct = {for (final price in agreed) price.productId: price.unitPrice};
    final catalogue = await allProducts();

    return {
      for (final product in catalogue)
        product.id: byProduct[product.id] ?? product.defaultPrice,
    };
  }

  // ---------- meta ----------

  Future<String?> readMeta(String key) async {
    final row = await (select(meta)..where((m) => m.key.equals(key))).getSingleOrNull();

    return row?.value;
  }

  Future<void> writeMeta(String key, String value) =>
      into(meta).insertOnConflictUpdate(MetaCompanion.insert(key: key, value: value));

  Future<void> clearMeta(String key) => (delete(meta)..where((m) => m.key.equals(key))).go();

  // ---------- the outbox ----------

  Future<void> enqueue({
    required String clientRequestId,
    required String type,
    required Map<String, dynamic> payload,
    required DateTime recordedAt,
    required String summary,
  }) =>
      into(outboxEntries).insert(OutboxEntriesCompanion.insert(
        clientRequestId: clientRequestId,
        type: type,
        payload: jsonEncode(payload),
        recordedAt: recordedAt,
        summary: summary,
      ));

  /// What is worth sending now: never tried, or failed transiently and due for another go.
  /// Oldest first, because a payment must not reach the server before the bill it settles.
  /// What the next push sends, oldest first. [ignoreBackoff] sends everything still waiting, retry
  /// wait or not - for when a person asks, rather than the background timer. Failed rows are never
  /// included: they wait for a person to put them back.
  Future<List<OutboxEntry>> dueEntries(DateTime now, {bool ignoreBackoff = false}) {
    final query = select(outboxEntries)
      ..where((e) => e.status.isIn([OutboxStatus.pending.stored, OutboxStatus.syncing.stored]))
      ..orderBy([(e) => OrderingTerm(expression: e.recordedAt)]);

    if (!ignoreBackoff) {
      query.where((e) => e.nextAttemptAt.isSmallerOrEqualValue(now) | e.nextAttemptAt.isNull());
    }

    return query.get();
  }

  Future<List<OutboxEntry>> entriesWithStatus(OutboxStatus status) =>
      (select(outboxEntries)
            ..where((e) => e.status.equals(status.stored))
            ..orderBy([(e) => OrderingTerm(expression: e.recordedAt)]))
          .get();

  /// Everything still waiting or stuck, newest first, for the status screen.
  Stream<List<OutboxEntry>> watchUnfinished() => (select(outboxEntries)
        ..where((e) => e.status.equals(OutboxStatus.synced.stored).not())
        ..orderBy([(e) => OrderingTerm(expression: e.recordedAt, mode: OrderingMode.desc)]))
      .watch();

  /// Everything of one kind this phone has recorded, newest first - synced or not.
  Stream<List<OutboxEntry>> watchOutboxOfType(String type) => (select(outboxEntries)
        ..where((e) => e.type.equals(type))
        ..orderBy([(e) => OrderingTerm(expression: e.recordedAt, mode: OrderingMode.desc)]))
      .watch();

  /// Shops this phone has recorded something for lately, newest first.
  ///
  /// Derived from the outbox because that is the only record of where the salesperson has actually
  /// been - and it works with no signal, which a server-side "recent shops" would not. Entries that
  /// are not about a shop (a van load, a stock request) yield a name that matches no customer and
  /// fall out when the caller looks them up.
  Future<List<String>> recentShopNames({int limit = 5}) async {
    final entries = await (select(outboxEntries)
          ..orderBy([(e) => OrderingTerm(expression: e.recordedAt, mode: OrderingMode.desc)])
          ..limit(120))
        .get();

    final names = <String>[];

    for (final entry in entries) {
      final name = entry.summary.split('·').first.trim();

      if (name.isEmpty || names.contains(name)) continue;

      names.add(name);
      if (names.length >= limit) break;
    }

    return names;
  }

  Stream<int> watchPendingCount() => watchUnfinished().map((rows) => rows.length);

  Future<void> markSynced(String clientRequestId, String? serverRecordId) =>
      (update(outboxEntries)..where((e) => e.clientRequestId.equals(clientRequestId))).write(
        OutboxEntriesCompanion(
          status: Value(OutboxStatus.synced.stored),
          serverRecordId: Value(serverRecordId),
          lastError: const Value(null),
          nextAttemptAt: const Value(null),
        ),
      );

  /// A business rule refused it. Keep it, stop retrying, and let the salesperson see why.
  Future<void> markFailed(String clientRequestId, String error) =>
      (update(outboxEntries)..where((e) => e.clientRequestId.equals(clientRequestId))).write(
        OutboxEntriesCompanion(
          status: Value(OutboxStatus.failed.stored),
          lastError: Value(error),
          nextAttemptAt: const Value(null),
        ),
      );

  /// The signal went, or the server was unwell. Try again later; the row is untouched otherwise.
  Future<void> markForRetry(String clientRequestId, int attemptCount, DateTime nextAttemptAt,
          String error) =>
      (update(outboxEntries)..where((e) => e.clientRequestId.equals(clientRequestId))).write(
        OutboxEntriesCompanion(
          status: Value(OutboxStatus.pending.stored),
          attemptCount: Value(attemptCount),
          nextAttemptAt: Value(nextAttemptAt),
          lastError: Value(error),
        ),
      );

  /// Puts a failed entry back in the queue, for the "try again" button.
  Future<void> retryNow(String clientRequestId) =>
      (update(outboxEntries)..where((e) => e.clientRequestId.equals(clientRequestId))).write(
        OutboxEntriesCompanion(
          status: Value(OutboxStatus.pending.stored),
          attemptCount: const Value(0),
          nextAttemptAt: const Value(null),
          lastError: const Value(null),
        ),
      );

  /// Signing out must never throw away work the server has not seen.
  Future<void> clearForSignOut() => transaction(() async {
        await delete(customers).go();
        await delete(products).go();
        await delete(customerPrices).go();
        await delete(payments).go();
        await delete(branches).go();
        await (delete(outboxEntries)
              ..where((e) => e.status.equals(OutboxStatus.synced.stored)))
            .go();
      });
}
