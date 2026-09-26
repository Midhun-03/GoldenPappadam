import 'dart:convert';

import 'package:drift/drift.dart';
import 'package:uuid/uuid.dart';

import 'local/database.dart';

const _uuid = Uuid();

/// A shop as the salesperson enters it. No opening balance, GSTIN or notes: a shop the salesperson
/// found owes nothing yet, and the rest is the office's.
class ShopDetails {
  const ShopDetails({
    required this.name,
    this.contactPerson,
    this.phone,
    this.address,
    this.hasMultipleBranches = false,
  });

  final String name;
  final String? contactPerson;
  final String? phone;
  final String? address;
  final bool hasMultipleBranches;

  Map<String, dynamic> toPayload(String id) => {
        'id': id,
        'name': name.trim(),
        'contactPerson': _blankToNull(contactPerson),
        'phone': _blankToNull(phone),
        'address': _blankToNull(address),
        'hasMultipleBranches': hasMultipleBranches,
      };
}

class BranchDetails {
  const BranchDetails({required this.name, this.location, this.address, this.phone, this.contactPerson});

  final String name;
  final String? location;
  final String? address;
  final String? phone;
  final String? contactPerson;

  Map<String, dynamic> toPayload(String id, String customerId) => {
        'id': id,
        'customerId': customerId,
        'name': name.trim(),
        'location': _blankToNull(location),
        'address': _blankToNull(address),
        'phone': _blankToNull(phone),
        'contactPerson': _blankToNull(contactPerson),
      };
}

/// One rate for one product, as entered on the new-shop form.
class RateEntry {
  const RateEntry({required this.productId, required this.productName, required this.unitPrice});

  final String productId;
  final String productName;
  final double unitPrice;
}

/// Shops the salesperson found, their branches and what they pay (CLAUDE.md §4, 2026-09-23).
///
/// Every change goes to the outbox like a sale, with an id made here, so a shop created with no
/// signal can be priced and billed straight away and a retry never makes a second one. The cache is
/// updated at the same moment so the rest of the app sees the change before the office does; after
/// a sync, [PendingShopChanges.reapplyPendingShopChanges] puts back anything still on its way.
///
/// What the salesperson cannot do has no method here: set an opening balance, close a shop or a
/// branch, or remove a rate. The server refuses those from a phone anyway.
class ShopsRepository {
  ShopsRepository(this._db);

  final AppDatabase _db;

  /// An existing shop with this exact name, ignoring case and spaces - the one to use instead.
  Future<CachedCustomer?> findByName(String name) async {
    final wanted = _normalise(name);
    if (wanted.isEmpty) return null;

    final all = await _db.allCustomers();
    return all.where((shop) => _normalise(shop.name) == wanted).firstOrNull;
  }

  /// A known shop whose name is inside what was typed: "Danya Supermarket Coimbatore" is most likely
  /// a branch of Danya Supermarket rather than a new customer.
  Future<CachedCustomer?> likelyParentOf(String typed) async {
    final text = _normalise(typed);
    if (text.isEmpty) return null;

    final all = await _db.allCustomers();
    final matches = all.where((shop) {
      final name = _normalise(shop.name);
      return name.length >= 4 && name != text && text.contains(name);
    }).toList()
      ..sort((a, b) => b.name.length.compareTo(a.name.length));

    return matches.firstOrNull;
  }

  /// A new shop and, optionally, its first rates - written together, the shop first. The outbox
  /// sends in the order rows were written, so the server has the shop before it is asked to price it.
  Future<String> createShop(ShopDetails details, {List<RateEntry> rates = const []}) async {
    if (details.name.trim().isEmpty) {
      throw ArgumentError('A shop needs a name.');
    }

    final existing = await findByName(details.name);
    if (existing != null) {
      throw StateError('${existing.name} is already a shop. Open it instead of adding it again.');
    }

    final id = _uuid.v4();
    final recordedAt = DateTime.now().toUtc();
    final name = details.name.trim();

    await _db.transaction(() async {
      await _db.enqueue(
        clientRequestId: _uuid.v4(),
        type: 'Customer',
        recordedAt: recordedAt,
        summary: '$name · new shop',
        payload: {'customer': details.toPayload(id)},
      );
      await _applyCustomer(details.toPayload(id));

      for (final rate in rates) {
        await _enqueueRate(id, name, rate.productId, rate.productName, rate.unitPrice, recordedAt);
      }
    });

    return id;
  }

  /// New details for a shop. The balance and GST status are the office's and are left alone.
  Future<void> updateShop(CachedCustomer shop, ShopDetails details) async {
    if (details.name.trim().isEmpty) {
      throw ArgumentError('A shop needs a name.');
    }

    final clash = await findByName(details.name);
    if (clash != null && clash.id != shop.id) {
      throw StateError('${clash.name} is already a shop.');
    }

    await _db.transaction(() async {
      await _db.enqueue(
        clientRequestId: _uuid.v4(),
        type: 'Customer',
        recordedAt: DateTime.now().toUtc(),
        summary: '${details.name.trim()} · details changed',
        payload: {'customer': details.toPayload(shop.id)},
      );
      await _applyCustomer(details.toPayload(shop.id));
    });
  }

  /// A branch under a multi-branch shop, new ([branchId] null) or edited.
  Future<String> saveBranch(CachedCustomer shop, BranchDetails details, {String? branchId}) async {
    if (details.name.trim().isEmpty) {
      throw ArgumentError('A branch needs a name.');
    }

    final id = branchId ?? _uuid.v4();
    final payload = details.toPayload(id, shop.id);

    await _db.transaction(() async {
      await _db.enqueue(
        clientRequestId: _uuid.v4(),
        type: 'CustomerBranch',
        recordedAt: DateTime.now().toUtc(),
        summary: '${shop.name} · ${details.name.trim()} branch ${branchId == null ? 'added' : 'changed'}',
        payload: {'branch': payload},
      );
      await _applyBranch(payload);
    });

    return id;
  }

  /// What the shop pays for one product from its next bill on, at every branch. Recorded in the
  /// office's price history with this salesperson's name. A bill already made is never re-priced.
  Future<void> setRate(CachedCustomer shop, CachedProduct product, double unitPrice) => setRates(
      shop, [RateEntry(productId: product.id, productName: product.name, unitPrice: unitPrice)]);

  /// Several rates for one shop, saved together: all of them or none.
  Future<void> setRates(CachedCustomer shop, List<RateEntry> rates) async {
    if (rates.any((rate) => rate.unitPrice <= 0)) {
      throw ArgumentError('A rate must be more than zero.');
    }

    final recordedAt = DateTime.now().toUtc();

    await _db.transaction(() async {
      for (final rate in rates) {
        await _enqueueRate(shop.id, shop.name, rate.productId, rate.productName, rate.unitPrice, recordedAt);
      }
    });
  }

  Future<void> _enqueueRate(String customerId, String customerName, String productId, String productName,
      double unitPrice, DateTime recordedAt) async {
    final payload = {'customerId': customerId, 'productId': productId, 'unitPrice': unitPrice};

    await _db.enqueue(
      clientRequestId: _uuid.v4(),
      type: 'CustomerPrice',
      recordedAt: recordedAt,
      summary: '$customerName · $productName rate ₹${unitPrice.toStringAsFixed(2)}',
      payload: {'customerPrice': payload},
    );
    await _applyPrice(payload);
  }

  Future<void> _applyCustomer(Map<String, dynamic> customer) => _db.applyCustomer(customer);

  Future<void> _applyBranch(Map<String, dynamic> branch) => _db.applyBranch(branch);

  Future<void> _applyPrice(Map<String, dynamic> price) => _db.applyPrice(price);

  static String _normalise(String value) => value.trim().toLowerCase().replaceAll(RegExp(r'\s+'), ' ');
}

/// Writes a pending shop, branch or rate into the cache, and puts them back after a snapshot.
extension PendingShopChanges on AppDatabase {
  Future<void> applyCustomer(Map<String, dynamic> customer) async {
    final id = customer['id'] as String;
    final fields = CustomersCompanion(
      name: Value(customer['name'] as String),
      contactPerson: Value(customer['contactPerson'] as String?),
      phone: Value(customer['phone'] as String?),
      address: Value(customer['address'] as String?),
      hasMultipleBranches: Value(customer['hasMultipleBranches'] as bool? ?? false),
    );

    final updated = await (update(customers)..where((c) => c.id.equals(id))).write(fields);

    // A new shop owes nothing, and is a normal-bill shop until the office says otherwise.
    if (updated == 0) {
      await into(customers).insert(fields.copyWith(id: Value(id), balance: const Value(0)));
    }
  }

  Future<void> applyBranch(Map<String, dynamic> branch) => into(branches).insertOnConflictUpdate(
        BranchesCompanion.insert(
          id: branch['id'] as String,
          customerId: branch['customerId'] as String,
          name: branch['name'] as String,
          location: Value(branch['location'] as String?),
          address: Value(branch['address'] as String?),
          phone: Value(branch['phone'] as String?),
          contactPerson: Value(branch['contactPerson'] as String?),
        ),
      );

  Future<void> applyPrice(Map<String, dynamic> price) => into(customerPrices).insertOnConflictUpdate(
        CustomerPricesCompanion.insert(
          customerId: price['customerId'] as String,
          productId: price['productId'] as String,
          unitPrice: (price['unitPrice'] as num).toDouble(),
        ),
      );

  /// A snapshot is the office's view, and it does not yet include what this phone has not sent -
  /// so after replacing the cache, the shops, branches and rates still waiting are put back on top,
  /// oldest first. Without this a sync that pulled but could not push would make a shop the
  /// salesperson created an hour ago vanish from the list.
  Future<void> reapplyPendingShopChanges() async {
    final waiting = await (select(outboxEntries)
          ..where((e) =>
              e.type.isIn(['Customer', 'CustomerBranch', 'CustomerPrice']) &
              e.status.isIn([OutboxStatus.pending.stored, OutboxStatus.syncing.stored]))
          ..orderBy([
            (e) => OrderingTerm(expression: e.recordedAt),
            (e) => OrderingTerm(expression: const CustomExpression<int>('rowid')),
          ]))
        .get();

    for (final entry in waiting) {
      final payload = jsonDecode(entry.payload) as Map<String, dynamic>;

      switch (entry.type) {
        case 'Customer':
          await applyCustomer(payload['customer'] as Map<String, dynamic>);
        case 'CustomerBranch':
          await applyBranch(payload['branch'] as Map<String, dynamic>);
        case 'CustomerPrice':
          await applyPrice(payload['customerPrice'] as Map<String, dynamic>);
      }
    }
  }
}

String? _blankToNull(String? value) => (value == null || value.trim().isEmpty) ? null : value.trim();
