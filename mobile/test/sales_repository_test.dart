import 'dart:convert';

import 'package:drift/drift.dart' hide isNotNull, isNull;
import 'package:drift/native.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:golden_pappadam_sales/data/local/database.dart';
import 'package:golden_pappadam_sales/data/sales_repository.dart';

/// What the salesperson does at a shop, and what ends up in the outbox because of it.
///
/// The payload shapes here have to match the server's SubmissionItemRequest exactly - the sync
/// engine spreads them straight into the request - so these tests are as much a contract with the
/// API as they are a check on this file.
void main() {
  late AppDatabase db;
  late SalesRepository repository;

  setUp(() async {
    db = AppDatabase(NativeDatabase.memory());
    repository = SalesRepository(db);

    await db.replaceSnapshot(
      customers: [shopRow(id: 'shop-1', name: 'Kumar Stores', balance: 10000)],
      products: [
        ProductsCompanion.insert(
            id: 'p1',
            productCode: 'PKT-20',
            name: '20 piece packet',
            unitCode: 'PKT',
            defaultPrice: const Value(45)),
      ],
      prices: [
        CustomerPricesCompanion.insert(customerId: 'shop-1', productId: 'p1', unitPrice: 35),
      ],
    );
  });

  tearDown(() => db.close());

  Future<Map<String, dynamic>> payloadOf(OutboxEntry entry) async =>
      jsonDecode(entry.payload) as Map<String, dynamic>;

  const line = SaleLine(
    productId: 'p1',
    productName: '20 piece packet',
    quantity: 10,
    unitPrice: 35,
  );

  test('a credit sale writes the bill and the visit, and no payment', () async {
    await repository.recordSale(
      customerId: 'shop-1',
      customerName: 'Kumar Stores',
      lines: [line],
      amountPaid: 0,
      paymentMethod: 'Cash',
    );

    final entries = await db.select(db.outboxEntries).get();

    expect(entries.map((e) => e.type), containsAll(['Invoice', 'Visit']));
    expect(entries.where((e) => e.type == 'Payment'), isEmpty);
  });

  test('a sale paid on the spot writes all three, and the visit names the other two', () async {
    await repository.recordSale(
      customerId: 'shop-1',
      customerName: 'Kumar Stores',
      lines: [line],
      amountPaid: 350,
      paymentMethod: 'Cash',
    );

    final entries = await db.select(db.outboxEntries).get();
    expect(entries, hasLength(3));

    final sale = entries.firstWhere((e) => e.type == 'Invoice');
    final payment = entries.firstWhere((e) => e.type == 'Payment');
    final visit = await payloadOf(entries.firstWhere((e) => e.type == 'Visit'));

    // The phone has never spoken to the server, so it names them by the only ids it has.
    expect(visit['visit']['saleClientRequestId'], sale.clientRequestId);
    expect(visit['visit']['paymentClientRequestId'], payment.clientRequestId);
    expect(visit['visit']['outcome'], 'Sold');
  });

  test('the sale payload carries the price that was charged, line by line', () async {
    await repository.recordSale(
      customerId: 'shop-1',
      customerName: 'Kumar Stores',
      lines: [line],
      amountPaid: 0,
      paymentMethod: 'Cash',
      pricesAsOf: '2026-09-15T08:00:00.000Z',
    );

    final sale = await payloadOf(
        (await db.select(db.outboxEntries).get()).firstWhere((e) => e.type == 'Invoice'));

    final lines = sale['sale']['lines'] as List;
    expect(lines, hasLength(1));
    expect(lines.single['productId'], 'p1');
    expect(lines.single['quantity'], 10.0);
    expect(lines.single['unitPrice'], 35.0);

    // The stamp the office needs to tell whether the price moved while the phone was away.
    expect(sale['sale']['pricesAsOf'], '2026-09-15T08:00:00.000Z');
    expect(sale['sale']['customerId'], 'shop-1');
  });

  test('a part payment records what was actually handed over, not the bill', () async {
    await repository.recordSale(
      customerId: 'shop-1',
      customerName: 'Kumar Stores',
      lines: [line],
      amountPaid: 200,
      paymentMethod: 'UPI',
    );

    final payment = await payloadOf(
        (await db.select(db.outboxEntries).get()).firstWhere((e) => e.type == 'Payment'));

    expect(payment['payment']['amount'], 200.0);
    expect(payment['payment']['method'], 'UPI');
  });

  test('money against an old balance needs no sale at all', () async {
    await repository.recordPayment(
      customerId: 'shop-1',
      customerName: 'Kumar Stores',
      amount: 5000,
      method: 'Cash',
    );

    final entries = await db.select(db.outboxEntries).get();

    expect(entries.where((e) => e.type == 'Invoice'), isEmpty);

    final payment = await payloadOf(entries.firstWhere((e) => e.type == 'Payment'));
    expect(payment['payment']['amount'], 5000.0);
  });

  test('a shop that needed nothing is still a recorded visit', () async {
    await repository.recordVisit(
      customerId: 'shop-1',
      customerName: 'Kumar Stores',
      outcome: 'NoOrder',
      notes: 'Still had stock',
    );

    final visit = await payloadOf((await db.select(db.outboxEntries).get()).single);

    expect(visit['visit']['outcome'], 'NoOrder');
    expect(visit['visit']['saleClientRequestId'], isNull);
    expect(visit['visit']['notes'], 'Still had stock');
  });

  test('every entry gets its own id, so two sales are never mistaken for one', () async {
    await repository.recordSale(
      customerId: 'shop-1',
      customerName: 'Kumar Stores',
      lines: [line],
      amountPaid: 0,
      paymentMethod: 'Cash',
    );
    await repository.recordSale(
      customerId: 'shop-1',
      customerName: 'Kumar Stores',
      lines: [line],
      amountPaid: 0,
      paymentMethod: 'Cash',
    );

    final ids = (await db.select(db.outboxEntries).get()).map((e) => e.clientRequestId).toSet();

    expect(ids, hasLength(4));
  });

  test('a sale with nothing on it is refused before anything is written', () async {
    expect(
      () => repository.recordSale(
        customerId: 'shop-1',
        customerName: 'Kumar Stores',
        lines: const [],
        amountPaid: 0,
        paymentMethod: 'Cash',
      ),
      throwsArgumentError,
    );

    expect(await db.select(db.outboxEntries).get(), isEmpty);
  });

  test('everything is queued the moment it is saved, with no network anywhere', () async {
    await repository.recordSale(
      customerId: 'shop-1',
      customerName: 'Kumar Stores',
      lines: [line],
      amountPaid: 350,
      paymentMethod: 'Cash',
    );

    final waiting = await db.entriesWithStatus(OutboxStatus.pending);

    expect(waiting, hasLength(3));
    expect(waiting.every((e) => e.serverRecordId == null), isTrue);
  });

  test('searching finds a shop by part of its name or its phone number', () async {
    await db.replaceSnapshot(
      customers: [
        shopRow(id: 's1', name: 'Kumar Stores', balance: 0, phone: '9847012345'),
        shopRow(id: 's2', name: 'Anand Bakery', balance: 0),
      ],
      products: const [],
      prices: const [],
    );

    expect((await db.searchShops('kum')).single.name, 'Kumar Stores');
    expect((await db.searchShops('9847')).single.name, 'Kumar Stores');
    expect(await db.searchShops('zzz'), isEmpty);
    expect(await db.searchShops(''), hasLength(2));
  });
}
