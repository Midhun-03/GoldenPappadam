import 'dart:convert';

import 'package:drift/drift.dart' hide isNotNull, isNull;
import 'package:drift/native.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:golden_pappadam_sales/data/local/database.dart';
import 'package:golden_pappadam_sales/data/sales_repository.dart';
import 'package:golden_pappadam_sales/data/shops_repository.dart';

/// Shops the salesperson finds, their branches and rates. The payload shapes are the server's
/// exactly - MobileContractTests posts the same JSON to the real API - and the cache is checked
/// because a shop made offline has to be billable at once.
void main() {
  late AppDatabase db;
  late ShopsRepository shops;

  const packet = ProductsCompanion(
    id: Value('p1'),
    productCode: Value('PKT-20'),
    name: Value('20 piece packet'),
    unitCode: Value('PKT'),
    defaultPrice: Value(45),
  );

  Future<void> snapshot({List<CustomersCompanion> customers = const []}) => db.replaceSnapshot(
        customers: customers,
        products: [packet],
        prices: const [],
      );

  setUp(() async {
    db = AppDatabase(NativeDatabase.memory());
    shops = ShopsRepository(db);
    await snapshot(customers: [
      shopRow(id: 'danya', name: 'Danya Supermarket', balance: 500, hasMultipleBranches: true, gstin: '32ABCDE1234F1Z5'),
    ]);
  });

  tearDown(() => db.close());

  Future<List<OutboxEntry>> sent() => db.dueEntries(DateTime.now().toUtc(), ignoreBackoff: true);

  Map<String, dynamic> body(OutboxEntry entry) => jsonDecode(entry.payload) as Map<String, dynamic>;

  test('a new shop carries its first rates inside it, and can be billed at once', () async {
    final id = await shops.createShop(
      const ShopDetails(name: '  Anand Bakery ', phone: '9847012345', address: ' '),
      rates: const [RateEntry(productId: 'p1', productName: '20 piece packet', unitPrice: 38)],
    );

    // One submission: the office creates the shop and its rates together, or neither.
    final entries = await sent();
    expect(entries.map((e) => e.type), ['Customer']);

    expect(body(entries[0])['customer'], {
      'id': id,
      'name': 'Anand Bakery',
      'contactPerson': null,
      'phone': '9847012345',
      'address': null,
      'hasMultipleBranches': false,
      'initialRates': [
        {'productId': 'p1', 'unitPrice': 38.0}
      ],
    });

    // Already on the phone: a normal-bill shop that owes nothing, at the rate just agreed.
    final cached = await db.findCustomer(id);
    expect(cached!.name, 'Anand Bakery');
    expect(cached.balance, 0);
    expect(cached.gstin, isNull);
    expect((await db.pricesFor(id))['p1'], 38);
  });

  test('a shop the phone already has is never added twice, whatever the case or spacing', () async {
    expect(
      () => shops.createShop(const ShopDetails(name: 'danya  SUPERMARKET')),
      throwsA(isA<StateError>()),
    );
    expect(await sent(), isEmpty);
  });

  test('a name containing a known shop points at that shop, to add a branch there', () async {
    expect((await shops.likelyParentOf('Danya Supermarket Coimbatore'))?.id, 'danya');
    expect(await shops.likelyParentOf('Danya Supermarket'), isNull);
    expect(await shops.likelyParentOf('Kumar Stores'), isNull);
  });

  test('an edit keeps what the office owns: the balance and the GST registration', () async {
    final danya = (await db.findCustomer('danya'))!;

    await shops.updateShop(
      danya,
      const ShopDetails(name: 'Danya Supermarket', contactPerson: 'Anil', hasMultipleBranches: true),
    );

    final cached = (await db.findCustomer('danya'))!;
    expect(cached.contactPerson, 'Anil');
    expect(cached.balance, 500);
    expect(cached.gstin, '32ABCDE1234F1Z5');

    final payload = body((await sent()).single)['customer'] as Map<String, dynamic>;
    expect(payload.keys, isNot(contains('openingBalance')));
    expect(payload.keys, isNot(contains('gstin')));
  });

  test('a branch goes under its shop, new or edited, with the same id both times', () async {
    final danya = (await db.findCustomer('danya'))!;

    final id = await shops.saveBranch(danya, const BranchDetails(name: 'Coimbatore', location: 'Coimbatore'));
    await shops.saveBranch(danya, const BranchDetails(name: 'Coimbatore', phone: '0422 000'), branchId: id);

    final entries = await sent();
    expect(entries.map((e) => e.type), ['CustomerBranch', 'CustomerBranch']);
    expect(body(entries[0])['branch'], {
      'id': id,
      'customerId': 'danya',
      'name': 'Coimbatore',
      'location': 'Coimbatore',
      'address': null,
      'phone': null,
      'contactPerson': null,
    });

    final branches = await db.branchesFor('danya');
    expect(branches.single.phone, '0422 000');
  });

  test('changing a rate is a request: the price the phone bills at stays until the office agrees', () async {
    final danya = (await db.findCustomer('danya'))!;

    await shops.requestRates(danya, const [RateEntry(productId: 'p1', productName: '20 piece packet', unitPrice: 37)],
        reason: 'Festival offer');

    // Still the standard 45: nothing is billed at 37 until it is approved.
    expect((await db.pricesFor('danya'))['p1'], 45);

    final entry = (await sent()).single;
    expect(entry.type, 'RateRequest');
    final request = body(entry)['rateRequest'] as Map<String, dynamic>;
    expect(request.keys, containsAll(['id', 'customerId', 'productId', 'requestedPrice', 'reason']));
    expect((request['customerId'], request['productId'], request['requestedPrice'], request['reason']),
        ('danya', 'p1', 37.0, 'Festival offer'));

    final cached = (await db.rateRequestsFor('danya')).single;
    expect((cached.id, cached.status, cached.requestedPrice), (request['id'], 'Pending', 37.0));
  });

  test('a newer request replaces the waiting one, and a waiting one can be withdrawn', () async {
    final danya = (await db.findCustomer('danya'))!;
    const first = RateEntry(productId: 'p1', productName: '20 piece packet', unitPrice: 37);
    const second = RateEntry(productId: 'p1', productName: '20 piece packet', unitPrice: 38);

    await shops.requestRates(danya, const [first]);
    await shops.requestRates(danya, const [second]);

    var requests = await db.rateRequestsFor('danya');
    expect(requests.where((r) => r.status == 'Pending').single.requestedPrice, 38);
    expect(requests.where((r) => r.status == 'Cancelled').single.requestedPrice, 37);

    await shops.cancelRequest(danya, requests.firstWhere((r) => r.status == 'Pending'));

    requests = await db.rateRequestsFor('danya');
    expect(requests.every((r) => r.status == 'Cancelled'), isTrue);
    expect((await sent()).last.type, 'RateRequestCancel');
    expect(() => shops.requestRates(danya, const []), throwsArgumentError);
  });

  test('a request still on its way survives a snapshot that does not know it yet', () async {
    final danya = (await db.findCustomer('danya'))!;
    await shops.requestRates(danya, const [RateEntry(productId: 'p1', productName: '20 piece packet', unitPrice: 37)]);

    await db.transaction(() async {
      await snapshot(customers: [shopRow(id: 'danya', name: 'Danya Supermarket', balance: 500)]);
      await db.reapplyPendingShopChanges();
    });

    expect((await db.rateRequestsFor('danya')).single.status, 'Pending');
    expect((await db.pricesFor('danya'))['p1'], 45);
  });

  test('a sync that brings the office list back keeps what this phone has not sent yet', () async {
    final id = await shops.createShop(
      const ShopDetails(name: 'Anand Bakery'),
      rates: const [RateEntry(productId: 'p1', productName: '20 piece packet', unitPrice: 38)],
    );

    // The office's snapshot does not know Anand Bakery yet.
    await db.transaction(() async {
      await snapshot(customers: [shopRow(id: 'danya', name: 'Danya Supermarket', balance: 500)]);
      await db.reapplyPendingShopChanges();
    });

    expect(await db.findCustomer(id), isNotNull);
    expect((await db.pricesFor(id))['p1'], 38);
  });

  test('once the office has it, the next snapshot is the only truth', () async {
    final id = await shops.createShop(const ShopDetails(name: 'Anand Bakery'));
    await db.markSynced((await sent()).single.clientRequestId, id);

    await db.transaction(() async {
      await snapshot(customers: [shopRow(id: id, name: 'Anand Bakery & Sons', balance: 120)]);
      await db.reapplyPendingShopChanges();
    });

    // The office renamed it; the phone's old details are not put back over that.
    expect((await db.findCustomer(id))!.name, 'Anand Bakery & Sons');
  });

  test('things saved in the same second go in the order they were written', () async {
    final at = DateTime.utc(2026, 9, 26, 10);
    for (final id in ['c', 'a', 'b']) {
      await db.enqueue(clientRequestId: id, type: 'Visit', payload: const {}, recordedAt: at, summary: id);
    }

    expect((await sent()).map((e) => e.clientRequestId), ['c', 'a', 'b']);
  });
}
