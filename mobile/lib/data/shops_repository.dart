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

  /// [initialRates] only when the shop is being created: it is the one time a salesperson sets a rate.
  Map<String, dynamic> toPayload(String id, {List<RateEntry> initialRates = const []}) => {
        'id': id,
        'name': name.trim(),
        'contactPerson': _blankToNull(contactPerson),
        'phone': _blankToNull(phone),
        'address': _blankToNull(address),
        'hasMultipleBranches': hasMultipleBranches,
        if (initialRates.isNotEmpty)
          'initialRates': [
            for (final rate in initialRates) {'productId': rate.productId, 'unitPrice': rate.unitPrice}
          ],
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

/// Shops the salesperson found, their branches and what they pay (CLAUDE.md §4, 2026-09-23 and
/// 2026-09-30).
///
/// Every change goes to the outbox like a sale, with an id made here, so a shop created with no
/// signal can be priced and billed straight away and a retry never makes a second one. The cache is
/// updated at the same moment so the rest of the app sees the change before the office does; after
/// a sync, [PendingShopChanges.reapplyPendingShopChanges] puts back anything still on its way.
///
/// Rates: a new shop's first rates travel inside the shop itself - the only time a salesperson sets
/// one. After that, [requestRates] asks the office, and the rate the phone bills at does not move until
/// the office approves and the next snapshot brings it.
///
/// What the salesperson cannot do has no method here: set an opening balance, close a shop or a
/// branch, or change or remove a rate directly. The server refuses those from a phone anyway.
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

  /// A new shop and, optionally, its first rates - one submission, so the office creates both or
  /// neither. The rates go into the phone's price list at once, so the shop can be billed straight away.
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

    if (rates.any((rate) => rate.unitPrice <= 0)) {
      throw ArgumentError('A rate must be more than zero.');
    }

    final payload = details.toPayload(id, initialRates: rates);

    await _db.transaction(() async {
      await _db.enqueue(
        clientRequestId: _uuid.v4(),
        type: 'Customer',
        recordedAt: recordedAt,
        summary: '$name · new shop',
        payload: {'customer': payload},
      );
      await _db.applyCustomer(payload);
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
      await _db.applyCustomer(details.toPayload(shop.id));
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
      await _db.applyBranch(payload);
    });

    return id;
  }

  /// Asks the office to change what [shop] pays - one request per product, sent together. Nothing
  /// changes on the phone's price list: the shop keeps its rate until the office approves. A request
  /// for a product that already has one waiting replaces it, here and at the office.
  Future<void> requestRates(CachedCustomer shop, List<RateEntry> rates, {String? reason}) async {
    if (rates.isEmpty) {
      throw ArgumentError('Choose at least one product and its new rate.');
    }

    if (rates.any((rate) => rate.unitPrice <= 0)) {
      throw ArgumentError('A rate must be more than zero.');
    }

    final recordedAt = DateTime.now().toUtc();

    await _db.transaction(() async {
      for (final rate in rates) {
        final payload = {
          'id': _uuid.v4(),
          'customerId': shop.id,
          'productId': rate.productId,
          'requestedPrice': rate.unitPrice,
          'reason': _blankToNull(reason),
        };

        await _db.enqueue(
          clientRequestId: _uuid.v4(),
          type: 'RateRequest',
          recordedAt: recordedAt,
          summary: '${shop.name} · ${rate.productName} ₹${rate.unitPrice.toStringAsFixed(2)} requested',
          payload: {'rateRequest': payload},
        );
        await _db.applyRateRequest(payload, recordedAt);
      }
    });
  }

  /// Withdraws a request the office has not decided yet.
  Future<void> cancelRequest(CachedCustomer shop, CachedRateRequest request) async {
    if (request.status != 'Pending') {
      throw StateError('The office has already decided this request.');
    }

    await _db.transaction(() async {
      await _db.enqueue(
        clientRequestId: _uuid.v4(),
        type: 'RateRequestCancel',
        recordedAt: DateTime.now().toUtc(),
        summary: '${shop.name} · rate request withdrawn',
        payload: {
          'rateRequestCancel': {'id': request.id}
        },
      );
      await _db.cancelRateRequest(request.id);
    });
  }

  static String _normalise(String value) => value.trim().toLowerCase().replaceAll(RegExp(r'\s+'), ' ');
}

/// Writes a pending shop, branch or rate request into the cache, and puts them back after a snapshot.
extension PendingShopChanges on AppDatabase {
  /// A shop, and - when it is being created - the first rates it carries.
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

    for (final rate in (customer['initialRates'] as List<dynamic>? ?? const []).cast<Map<String, dynamic>>()) {
      await applyPrice({'customerId': id, 'productId': rate['productId'], 'unitPrice': rate['unitPrice']});
    }
  }

  /// A request just made: waiting, and replacing any request still waiting for the same product.
  Future<void> applyRateRequest(Map<String, dynamic> request, DateTime requestedAt) async {
    final customerId = request['customerId'] as String;
    final productId = request['productId'] as String;

    await (update(rateRequests)
          ..where((r) =>
              r.customerId.equals(customerId) & r.productId.equals(productId) & r.status.equals('Pending')))
        .write(const RateRequestsCompanion(status: Value('Cancelled')));

    final current = await (select(customerPrices)
          ..where((p) => p.customerId.equals(customerId) & p.productId.equals(productId)))
        .getSingleOrNull();

    await into(rateRequests).insertOnConflictUpdate(RateRequestsCompanion.insert(
      id: request['id'] as String,
      customerId: customerId,
      productId: productId,
      requestedPrice: (request['requestedPrice'] as num).toDouble(),
      priceWhenRequested: Value(current?.unitPrice),
      status: 'Pending',
      requestedAt: requestedAt,
    ));
  }

  Future<void> cancelRateRequest(String id) => (update(rateRequests)..where((r) => r.id.equals(id)))
      .write(const RateRequestsCompanion(status: Value('Cancelled')));

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
  /// so after replacing the cache, the shops, branches and requests still waiting are put back on
  /// top, oldest first. Without this a sync that pulled but could not push would make a shop the
  /// salesperson created an hour ago vanish from the list.
  ///
  /// A rate from an older version of this app ('CustomerPrice') is not put back: the office records
  /// it as a request now, so showing it as the shop's rate would be wrong.
  Future<void> reapplyPendingShopChanges() async {
    final waiting = await (select(outboxEntries)
          ..where((e) =>
              e.type.isIn(['Customer', 'CustomerBranch', 'RateRequest', 'RateRequestCancel']) &
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
        case 'RateRequest':
          await applyRateRequest(payload['rateRequest'] as Map<String, dynamic>, entry.recordedAt);
        case 'RateRequestCancel':
          await cancelRateRequest((payload['rateRequestCancel'] as Map<String, dynamic>)['id'] as String);
      }
    }
  }
}

String? _blankToNull(String? value) => (value == null || value.trim().isEmpty) ? null : value.trim();
