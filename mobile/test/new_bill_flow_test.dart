import 'dart:convert';

import 'package:drift/drift.dart' hide isNotNull, isNull;
import 'package:drift/native.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:golden_pappadam_sales/app.dart';
import 'package:golden_pappadam_sales/core/money.dart';
import 'package:golden_pappadam_sales/data/local/database.dart';
import 'package:golden_pappadam_sales/data/sales_repository.dart';
import 'package:golden_pappadam_sales/features/sale/sale_screen.dart';

/// Three steps on one page: pick the shop, set a quantity on whatever it is priced for with the
/// +/- stepper, then say how it was paid. The sale then goes through the same repository and
/// outbox as ever.
void main() {
  late AppDatabase db;

  setUp(() async {
    db = AppDatabase(NativeDatabase.memory());

    await db.replaceSnapshot(
      customers: [
        shopRow(id: 'shop-1', name: 'Kumar Stores', balance: 10000, phone: '9847012345'),
        shopRow(id: 'shop-2', name: 'Anand Bakery', balance: 0),
        shopRow(id: 'shop-3', name: 'Danya Supermarket', balance: 0, hasMultipleBranches: true),
      ],
      branches: [
        branchRow(id: 'branch-1', customerId: 'shop-3', name: 'Kundara', location: 'Kollam'),
        branchRow(id: 'branch-2', customerId: 'shop-3', name: 'Coimbatore', location: 'Coimbatore'),
      ],
      products: [
        ProductsCompanion.insert(
            id: 'p1',
            productCode: 'PKT-20',
            name: '20 piece packet',
            unitCode: 'PKT',
            defaultPrice: const Value(45)),
        ProductsCompanion.insert(
            id: 'p2',
            productCode: 'PKT-6',
            name: '6 piece packet',
            unitCode: 'PKT',
            defaultPrice: const Value(15)),
        // No default price and no agreed price for shop-1: never offered on its bill.
        ProductsCompanion.insert(
            id: 'p3', productCode: 'LOOSE', name: 'Loose pappadam', unitCode: 'KG'),
      ],
      prices: [
        CustomerPricesCompanion.insert(customerId: 'shop-1', productId: 'p1', unitPrice: 35),
        CustomerPricesCompanion.insert(customerId: 'shop-3', productId: 'p1', unitPrice: 37),
      ],
    );
  });

  tearDown(() => db.close());

  /// The app's own providers, with the database swapped for an in-memory one. Nothing else is
  /// faked: the screens, the repository and the outbox are the real ones.
  Widget wrap(Widget child) => ProviderScope(
        overrides: [databaseProvider.overrideWithValue(db)],
        child: MaterialApp(home: child),
      );

  /// Pushes the bill page the way Home does, so popping after a save has somewhere to go.
  Future<void> openBill(WidgetTester tester, {CachedCustomer? shop}) async {
    await tester.pumpWidget(wrap(
      Builder(
        builder: (context) => Scaffold(
          body: Center(
            child: ElevatedButton(
              onPressed: () => Navigator.of(context).push(
                MaterialPageRoute<void>(builder: (_) => SaleScreen(shop: shop)),
              ),
              child: const Text('open bill'),
            ),
          ),
        ),
      ),
    ));

    await tester.tap(find.text('open bill'));
    await tester.pumpAndSettle();
  }

  Finder shopDropdown() => find.byType(DropdownMenu<CachedCustomer>);

  Finder fieldOf(Finder dropdown) =>
      find.descendant(of: dropdown, matching: find.byType(TextField));

  /// Types into the shop's searchable dropdown and taps the entry, as a salesperson would.
  Future<void> chooseShop(WidgetTester tester, {String typed = 'kum', String entry = 'Kumar Stores'}) async {
    final field = fieldOf(shopDropdown());
    await tester.tap(field);
    await tester.pumpAndSettle();
    await tester.enterText(field, typed);
    await tester.pumpAndSettle();
    await tester.tap(find.text(entry).last);
    await tester.pumpAndSettle();
  }

  /// The +/- stepper's plus button, scoped to one product's row so two rows never collide.
  Future<void> tapPlus(WidgetTester tester, String productId, {int times = 1}) async {
    final button = find.descendant(
      of: find.byKey(ValueKey('product-row-$productId')),
      matching: find.byIcon(Icons.add),
    );

    for (var i = 0; i < times; i++) {
      await tester.tap(button);
      await tester.pump();
    }
    await tester.pumpAndSettle();
  }

  Finder saveBar() => find.widgetWithText(FilledButton, 'Save Bill');

  testWidgets('shop, products and total are on one page', (tester) async {
    await openBill(tester);

    expect(find.text('New Bill'), findsOneWidget);
    expect(find.text('Total amount'), findsOneWidget);

    // Nothing can be saved until there is a shop and something on the bill.
    expect(tester.widget<FilledButton>(saveBar()).onPressed, isNull);
  });

  testWidgets('the shop dropdown filters as you type', (tester) async {
    await openBill(tester);

    await tester.tap(shopDropdown());
    await tester.pumpAndSettle();
    await tester.enterText(fieldOf(shopDropdown()), 'anand');
    await tester.pumpAndSettle();

    expect(find.text('Anand Bakery'), findsWidgets);
    expect(find.text('Kumar Stores'), findsNothing);
  });

  testWidgets('a shop can be found by its phone number', (tester) async {
    await openBill(tester);

    await chooseShop(tester, typed: '98470', entry: 'Kumar Stores');

    expect(find.textContaining('This shop already owes'), findsOneWidget);
  });

  testWidgets('a product the shop has no price for is never listed', (tester) async {
    await openBill(tester);
    await chooseShop(tester);

    expect(find.byKey(const ValueKey('product-row-p1')), findsOneWidget);
    expect(find.byKey(const ValueKey('product-row-p2')), findsOneWidget);
    expect(find.byKey(const ValueKey('product-row-p3')), findsNothing);
  });

  testWidgets('the plus button fills quantity 1 and totals at the shop price', (tester) async {
    await openBill(tester);
    await chooseShop(tester);
    await tapPlus(tester, 'p1');

    final quantity = find.byKey(const ValueKey('qty-p1'));
    expect(tester.widget<TextField>(quantity).controller!.text, '1');

    // The shop's agreed 35, not the product's default 45.
    expect(find.text(money(35)), findsOneWidget);

    await tapPlus(tester, 'p1', times: 2);

    expect(tester.widget<TextField>(quantity).controller!.text, '3');
    expect(find.text(money(105)), findsOneWidget);
    expect(tester.widget<FilledButton>(saveBar()).onPressed, isNotNull);
  });

  testWidgets('opened from a shop page, the shop is already chosen', (tester) async {
    final shop = await db.findCustomer('shop-1');
    await openBill(tester, shop: shop);

    expect(tester.widget<TextField>(fieldOf(shopDropdown())).controller!.text, 'Kumar Stores');
    expect(find.byKey(const ValueKey('product-row-p1')), findsOneWidget);
  });

  Future<void> billOnePacket(WidgetTester tester) async {
    await openBill(tester);
    await chooseShop(tester);
    await tapPlus(tester, 'p1');
  }

  testWidgets('credit is the default plan, and a bill saved on it lands in the outbox and nowhere else',
      (tester) async {
    await billOnePacket(tester);

    expect(find.text('Credit'), findsOneWidget);
    await tester.tap(saveBar());
    await tester.pumpAndSettle();

    final entries = await db.select(db.outboxEntries).get();

    // The bill and the stop that explains it; no payment, because it was left on credit.
    expect(entries.map((e) => e.type), containsAll(['Invoice', 'Visit']));
    expect(entries.where((e) => e.type == 'Payment'), isEmpty);
    expect(entries.every((e) => e.status == 'Pending'), isTrue);

    // The price came from the shop's agreed list, not from anything typed on screen.
    final sale = entries.firstWhere((e) => e.type == 'Invoice');
    final line = ((jsonDecode(sale.payload)['sale']['lines']) as List).single;
    expect(line['unitPrice'], 35.0);

    // Saving goes back to where the bill was opened from.
    expect(find.text('open bill'), findsOneWidget);
  });

  testWidgets('a bill paid on the spot records the money too', (tester) async {
    await billOnePacket(tester);

    await tester.tap(find.text('Paid'));
    await tester.pumpAndSettle();
    await tester.tap(saveBar());
    await tester.pumpAndSettle();

    final entries = await db.select(db.outboxEntries).get();
    final payment = entries.firstWhere((e) => e.type == 'Payment');

    expect(jsonDecode(payment.payload)['payment']['amount'], 35.0);
  });

  testWidgets('part payment records what was actually handed over', (tester) async {
    await billOnePacket(tester);

    await tester.tap(find.text('Part Paid'));
    await tester.pumpAndSettle();

    // Nothing can be saved until an amount is entered.
    expect(tester.widget<FilledButton>(saveBar()).onPressed, isNull);

    await tester.enterText(find.byKey(const ValueKey('partial-amount')), '20');
    await tester.pumpAndSettle();
    await tester.tap(saveBar());
    await tester.pumpAndSettle();

    final entries = await db.select(db.outboxEntries).get();
    final payment = entries.firstWhere((e) => e.type == 'Payment');
    final sale = entries.firstWhere((e) => e.type == 'Invoice');

    // 35 delivered, 20 handed over. The rest is outstanding, which the server works out.
    expect(jsonDecode(payment.payload)['payment']['amount'], 20.0);
    expect(((jsonDecode(sale.payload)['sale']['lines']) as List).single['quantity'], 1.0);
  });

  testWidgets('every bill gets its own client request id', (tester) async {
    for (var i = 0; i < 2; i++) {
      await billOnePacket(tester);
      await tester.tap(saveBar());
      await tester.pumpAndSettle();
    }

    final ids = (await db.select(db.outboxEntries).get()).map((e) => e.clientRequestId).toSet();

    // Two bills, two visits, four ids. The server tells them apart by these and by nothing else.
    expect(ids, hasLength(4));
  });

  Finder branchDropdown() => find.byType(DropdownMenu<CachedBranch>);

  testWidgets('a plain shop never shows a branch picker', (tester) async {
    await openBill(tester);
    await chooseShop(tester);

    expect(branchDropdown(), findsNothing);
  });

  testWidgets('a multi-branch shop requires a branch before saving', (tester) async {
    await openBill(tester);
    await chooseShop(tester, typed: 'danya', entry: 'Danya Supermarket');
    await tapPlus(tester, 'p1');

    expect(branchDropdown(), findsOneWidget);
    expect(tester.widget<FilledButton>(saveBar()).onPressed, isNull);

    final field = fieldOf(branchDropdown());
    await tester.tap(field);
    await tester.pumpAndSettle();
    await tester.tap(find.text('Kundara').last);
    await tester.pumpAndSettle();

    expect(tester.widget<FilledButton>(saveBar()).onPressed, isNotNull);

    await tester.tap(saveBar());
    await tester.pumpAndSettle();

    final sale = (await db.select(db.outboxEntries).get()).firstWhere((e) => e.type == 'Invoice');
    expect(jsonDecode(sale.payload)['sale']['branchId'], 'branch-1');
  });

  testWidgets('switching to a plain shop clears a previously chosen branch', (tester) async {
    await openBill(tester);
    await chooseShop(tester, typed: 'danya', entry: 'Danya Supermarket');

    final field = fieldOf(branchDropdown());
    await tester.tap(field);
    await tester.pumpAndSettle();
    await tester.tap(find.text('Kundara').last);
    await tester.pumpAndSettle();

    await chooseShop(tester, typed: 'anand', entry: 'Anand Bakery');

    expect(branchDropdown(), findsNothing);
  });
}
