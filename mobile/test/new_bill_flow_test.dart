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

/// One page, like the admin's: a searchable shop dropdown, a card of product lines, the total at the
/// bottom. The sale then goes through the same repository and outbox as ever.
void main() {
  late AppDatabase db;

  setUp(() async {
    db = AppDatabase(NativeDatabase.memory());

    await db.replaceSnapshot(
      customers: [
        shopRow(id: 'shop-1', name: 'Kumar Stores', balance: 10000, phone: '9847012345'),
        shopRow(id: 'shop-2', name: 'Anand Bakery', balance: 0),
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
      ],
      prices: [
        CustomerPricesCompanion.insert(customerId: 'shop-1', productId: 'p1', unitPrice: 35),
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

  Finder dropdown<T>(int index) => find.byType(DropdownMenu<T>).at(index);

  Finder fieldOf(Finder dropdown) =>
      find.descendant(of: dropdown, matching: find.byType(TextField));

  /// Types into a searchable dropdown and taps the entry, as a salesperson would.
  Future<void> choose(WidgetTester tester, Finder field, String typed, String entry) async {
    await tester.tap(field);
    await tester.pumpAndSettle();
    await tester.enterText(fieldOf(field), typed);
    await tester.pumpAndSettle();
    await tester.tap(find.text(entry).last);
    await tester.pumpAndSettle();
  }

  Future<void> chooseShop(WidgetTester tester) =>
      choose(tester, dropdown<CachedCustomer>(0), 'kum', 'Kumar Stores');

  Future<void> chooseProduct(WidgetTester tester, int line, String name) =>
      choose(tester, dropdown<String>(line), name.substring(0, 2), name);

  /// The sheet's own Save, not the one on the total bar behind it.
  Finder sheetButton(String label) => find.descendant(
        of: find.byType(BottomSheet),
        matching: find.widgetWithText(FilledButton, label),
      );

  Finder saveBar() => find.widgetWithText(FilledButton, 'Save');

  testWidgets('shop, products and total are on one page', (tester) async {
    await openBill(tester);

    expect(find.text('New bill'), findsOneWidget);
    expect(find.text('Products'), findsOneWidget);
    expect(find.text('Total'), findsOneWidget);

    // Nothing can be saved until there is a shop and something on the bill.
    expect(tester.widget<FilledButton>(saveBar()).onPressed, isNull);
  });

  testWidgets('the shop dropdown filters as you type', (tester) async {
    await openBill(tester);

    await tester.tap(dropdown<CachedCustomer>(0));
    await tester.pumpAndSettle();
    await tester.enterText(fieldOf(dropdown<CachedCustomer>(0)), 'anand');
    await tester.pumpAndSettle();

    expect(find.text('Anand Bakery'), findsWidgets);
    expect(find.text('Kumar Stores'), findsNothing);
  });

  testWidgets('a shop can be found by its phone number', (tester) async {
    await openBill(tester);

    await choose(tester, dropdown<CachedCustomer>(0), '98470', 'Kumar Stores');

    expect(find.textContaining('This shop already owes'), findsOneWidget);
  });

  testWidgets('choosing a product fills quantity 1 and totals at the shop price', (tester) async {
    await openBill(tester);
    await chooseShop(tester);
    await chooseProduct(tester, 0, '20 piece packet');

    final quantity = find.byKey(const ValueKey('qty-0'));
    expect(tester.widget<TextField>(quantity).controller!.text, '1');

    // Line total and the bar's total: the shop's agreed 35, not the product's 45.
    expect(find.text(money(35)), findsNWidgets(2));

    await tester.enterText(quantity, '3');
    await tester.pumpAndSettle();

    expect(find.text(money(105)), findsNWidgets(2));
    expect(tester.widget<FilledButton>(saveBar()).onPressed, isNotNull);
  });

  testWidgets('a product on one line is not offered on another', (tester) async {
    await openBill(tester);
    await chooseShop(tester);
    await chooseProduct(tester, 0, '20 piece packet');

    await tester.tap(find.text('Add product'));
    await tester.pumpAndSettle();

    final second = tester.widget<DropdownMenu<String>>(dropdown<String>(1));
    expect(second.dropdownMenuEntries.map((e) => e.value), ['p2']);
  });

  testWidgets('opened from a shop page, the shop is already chosen', (tester) async {
    final shop = await db.findCustomer('shop-1');
    await openBill(tester, shop: shop);

    expect(tester.widget<TextField>(fieldOf(dropdown<CachedCustomer>(0))).controller!.text,
        'Kumar Stores');
    expect(find.byType(DropdownMenu<String>), findsOneWidget);
  });

  Future<void> billOnePacket(WidgetTester tester) async {
    await openBill(tester);
    await chooseShop(tester);
    await chooseProduct(tester, 0, '20 piece packet');
    await tester.tap(saveBar());
    await tester.pumpAndSettle();
  }

  testWidgets('a bill saved on credit lands in the outbox and nowhere else', (tester) async {
    await billOnePacket(tester);
    await tester.tap(find.text('On credit'));
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
    await tester.tap(find.textContaining('now'));
    await tester.pumpAndSettle();

    final entries = await db.select(db.outboxEntries).get();
    final payment = entries.firstWhere((e) => e.type == 'Payment');

    expect(jsonDecode(payment.payload)['payment']['amount'], 35.0);
  });

  testWidgets('part payment records what was actually handed over', (tester) async {
    await billOnePacket(tester);
    await tester.tap(find.text('Paid part of it'));
    await tester.pumpAndSettle();

    await tester.enterText(
        find.descendant(of: find.byType(BottomSheet), matching: find.byType(TextField)), '20');
    await tester.tap(sheetButton('Save'));
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
      await tester.tap(find.text('On credit'));
      await tester.pumpAndSettle();
    }

    final ids = (await db.select(db.outboxEntries).get()).map((e) => e.clientRequestId).toSet();

    // Two bills, two visits, four ids. The server tells them apart by these and by nothing else.
    expect(ids, hasLength(4));
  });
}
