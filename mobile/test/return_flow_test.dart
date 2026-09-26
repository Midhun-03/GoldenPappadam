import 'dart:convert';

import 'package:drift/drift.dart' hide isNotNull, isNull;
import 'package:drift/native.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:golden_pappadam_sales/app.dart';
import 'package:golden_pappadam_sales/data/local/database.dart';
import 'package:golden_pappadam_sales/data/sales_repository.dart';
import 'package:golden_pappadam_sales/features/returns/return_screen.dart';

/// Expired or damaged packets collected at a shop. The payload is checked field by field because
/// the sync engine sends it to the server as it is: MobileContractTests holds the other half.
void main() {
  late AppDatabase db;

  setUp(() async {
    db = AppDatabase(NativeDatabase.memory());

    await db.replaceSnapshot(
      customers: [
        shopRow(id: 'shop-1', name: 'Kumar Stores', balance: 10000),
        shopRow(id: 'shop-3', name: 'Danya Supermarket', balance: 0, hasMultipleBranches: true),
      ],
      branches: [
        branchRow(id: 'branch-1', customerId: 'shop-3', name: 'Kundara', location: 'Kollam'),
      ],
      products: [
        ProductsCompanion.insert(
            id: 'p1', productCode: 'PKT-20', name: '20 piece packet', unitCode: 'PKT', defaultPrice: const Value(45)),
        ProductsCompanion.insert(
            id: 'p2', productCode: 'PKT-6', name: '6 piece packet', unitCode: 'PKT', defaultPrice: const Value(15)),
      ],
      prices: [
        CustomerPricesCompanion.insert(customerId: 'shop-1', productId: 'p1', unitPrice: 35),
      ],
    );
  });

  tearDown(() => db.close());

  Widget wrap(Widget child) => ProviderScope(
        overrides: [databaseProvider.overrideWithValue(db)],
        child: MaterialApp(home: child),
      );

  Future<CachedCustomer> shop(String id) async => (await db.findCustomer(id))!;

  Future<void> openReturn(WidgetTester tester, CachedCustomer shop) async {
    await tester.pumpWidget(wrap(
      Builder(
        builder: (context) => Scaffold(
          body: Center(
            child: ElevatedButton(
              onPressed: () => Navigator.of(context).push(
                MaterialPageRoute<void>(builder: (_) => ReturnScreen(shop: shop)),
              ),
              child: const Text('open return'),
            ),
          ),
        ),
      ),
    ));

    await tester.tap(find.text('open return'));
    await tester.pumpAndSettle();
  }

  Future<void> tapPlus(WidgetTester tester, String productId, {int times = 1}) async {
    final button = find.descendant(
      of: find.byKey(ValueKey('return-row-$productId')),
      matching: find.byIcon(Icons.add),
    );

    for (var i = 0; i < times; i++) {
      await tester.tap(button);
      await tester.pump();
    }
    await tester.pumpAndSettle();
  }

  Future<void> tapAndSettle(WidgetTester tester, Finder finder) async {
    await tester.ensureVisible(finder);
    await tester.pumpAndSettle();
    await tester.tap(finder);
    await tester.pumpAndSettle();
  }

  FilledButton saveButton(WidgetTester tester) =>
      tester.widget<FilledButton>(find.byKey(const ValueKey('save-return')));

  Future<Map<String, dynamic>> onlyReturn() async {
    final entries = await db.select(db.outboxEntries).get();
    expect(entries, hasLength(1));
    expect(entries.single.type, 'Return');
    return (jsonDecode(entries.single.payload) as Map<String, dynamic>)['return'] as Map<String, dynamic>;
  }

  testWidgets('what the shop buys is listed first, and nothing on the screen is money', (tester) async {
    await openReturn(tester, await shop('shop-1'));

    final first = tester.getTopLeft(find.text('20 piece packet'));
    final second = tester.getTopLeft(find.text('6 piece packet'));
    expect(first.dy, lessThan(second.dy));
    expect(find.textContaining('₹'), findsNothing);
  });

  testWidgets('saving waits for an answer about fresh packets, because it moves van stock', (tester) async {
    await db.writeMeta('van_location_id', 'van-1');
    await openReturn(tester, await shop('shop-1'));

    await tapPlus(tester, 'p1', times: 4);
    expect(saveButton(tester).onPressed, isNull);

    await tapAndSettle(tester, find.byKey(const ValueKey('replaced-yes')));
    expect(saveButton(tester).onPressed, isNotNull);
  });

  testWidgets('fresh packets from the van are recorded as a replacement, with the reason per product',
      (tester) async {
    await db.writeMeta('van_location_id', 'van-1');
    await openReturn(tester, await shop('shop-1'));

    await tapPlus(tester, 'p1', times: 4);
    await tapPlus(tester, 'p2', times: 1);
    await tapAndSettle(
      tester,
      find.descendant(of: find.byKey(const ValueKey('return-reason-p2')), matching: find.text('Damaged')),
    );
    await tapAndSettle(tester, find.byKey(const ValueKey('replaced-yes')));
    await tapAndSettle(tester, find.byKey(const ValueKey('save-return')));

    final payload = await onlyReturn();
    expect(payload['customerId'], 'shop-1');
    expect(payload['replacedFromVan'], isTrue);
    expect(payload.containsKey('branchId'), isFalse);
    expect(payload['lines'], [
      {'productId': 'p1', 'quantity': 4.0, 'reason': 'Expired'},
      {'productId': 'p2', 'quantity': 1.0, 'reason': 'Damaged'},
    ]);

    // Nothing the phone sends could price or credit it.
    expect(payload.keys, isNot(contains('unitRate')));
    expect(payload.keys, isNot(contains('creditAmount')));

    expect(find.text('open return'), findsOneWidget);
  });

  testWidgets('a phone with no van can only leave it to the office', (tester) async {
    await openReturn(tester, await shop('shop-1'));

    expect(find.byKey(const ValueKey('replaced-yes')), findsNothing);

    await tapPlus(tester, 'p1', times: 2);
    await tapAndSettle(tester, find.byKey(const ValueKey('save-return')));

    expect((await onlyReturn())['replacedFromVan'], isFalse);
  });

  testWidgets('a multi-branch shop must name the branch before saving', (tester) async {
    await openReturn(tester, await shop('shop-3'));

    await tapPlus(tester, 'p1', times: 1);
    expect(saveButton(tester).onPressed, isNull);

    final field = find.descendant(
      of: find.byType(DropdownMenu<CachedBranch>),
      matching: find.byType(TextField),
    );
    await tester.tap(field);
    await tester.pumpAndSettle();
    await tester.tap(find.text('Kundara').last);
    await tester.pumpAndSettle();

    await tapAndSettle(tester, find.byKey(const ValueKey('save-return')));
    expect((await onlyReturn())['branchId'], 'branch-1');
  });

  test('the repository refuses a return with nothing on it before anything is written', () async {
    final repository = SalesRepository(db);

    expect(
      () => repository.recordReturn(
          customerId: 'shop-1', customerName: 'Kumar Stores', lines: const [], replacedFromVan: false),
      throwsArgumentError,
    );
    expect(await db.select(db.outboxEntries).get(), isEmpty);
  });

  test('a return shows on the shop page as waiting, like a sale', () async {
    await SalesRepository(db).recordReturn(
      customerId: 'shop-1',
      customerName: 'Kumar Stores',
      lines: const [ReturnLine(productId: 'p1', productName: '20 piece packet', quantity: 5, reason: 'Expired')],
      replacedFromVan: false,
    );

    final waiting = await SalesRepository(db).unsentFor('Kumar Stores');
    expect(waiting.single.summary, 'Kumar Stores · 5 returned');
  });
}
