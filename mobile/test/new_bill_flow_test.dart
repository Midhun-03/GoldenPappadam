import 'dart:convert';

import 'package:drift/drift.dart' hide isNotNull, isNull;
import 'package:drift/native.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:golden_pappadam_sales/app.dart';
import 'package:golden_pappadam_sales/data/local/database.dart';
import 'package:golden_pappadam_sales/data/sales_repository.dart';
import 'package:golden_pappadam_sales/features/sale/sale_screen.dart';
import 'package:golden_pappadam_sales/features/shops/shop_picker_screen.dart';

/// Home to a saved bill, driven through the real widgets.
///
/// The sale itself still goes through the same repository and the same outbox as the shop page, so
/// what is worth proving here is the new route to it: the button opens the shop picker, the picker
/// hands a shop to the existing sale screen, and the search finds the right one.
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

  testWidgets('the shop picker lists shops and hands one back', (tester) async {
    CachedCustomer? picked;

    await tester.pumpWidget(wrap(
      Builder(
        builder: (context) => ElevatedButton(
          onPressed: () async {
            picked = await Navigator.of(context).push<CachedCustomer>(
              MaterialPageRoute(builder: (_) => const ShopPickerScreen()),
            );
          },
          child: const Text('open'),
        ),
      ),
    ));

    await tester.tap(find.text('open'));
    await tester.pumpAndSettle();

    expect(find.text('Which shop?'), findsOneWidget);
    expect(find.text('Kumar Stores'), findsOneWidget);
    expect(find.text('Anand Bakery'), findsOneWidget);

    await tester.tap(find.text('Kumar Stores'));
    await tester.pumpAndSettle();

    expect(picked?.id, 'shop-1');
  });

  testWidgets('the picker searches by name', (tester) async {
    await tester.pumpWidget(wrap(const ShopPickerScreen()));
    await tester.pumpAndSettle();

    await tester.enterText(find.byType(TextField).first, 'anand');
    await tester.pumpAndSettle();

    expect(find.text('Anand Bakery'), findsOneWidget);
    expect(find.text('Kumar Stores'), findsNothing);
  });

  /// Pushes the sale screen the way the app does, so popping after a save has somewhere to go.
  Future<void> openBill(WidgetTester tester, CachedCustomer shop) async {
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

  /// The sheet's own Save, not the one on the total bar behind it.
  Finder sheetButton(String label) => find.descendant(
        of: find.byType(BottomSheet),
        matching: find.widgetWithText(FilledButton, label),
      );

  Future<void> addPacket(WidgetTester tester) async {
    await tester.tap(find.text('Add product'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('20 piece packet').last);
    await tester.pumpAndSettle();
  }

  testWidgets('the sale screen offers only products this shop has a price for', (tester) async {
    final shop = await db.findCustomer('shop-1');
    await openBill(tester, shop!);

    await tester.tap(find.text('Add product'));
    await tester.pumpAndSettle();

    // p1 has an agreed price for this shop; p2 has only a product price, so it is sellable too.
    expect(find.text('20 piece packet'), findsOneWidget);
    expect(find.text('6 piece packet'), findsOneWidget);
  });

  testWidgets('picking the same product twice keeps one line', (tester) async {
    final shop = await db.findCustomer('shop-1');
    await openBill(tester, shop!);

    await addPacket(tester);
    await addPacket(tester);

    // One line, quantity two - not two lines for the same packet.
    expect(find.byType(TextField), findsOneWidget);
    expect(find.text('₹70.00'), findsWidgets);
  });

  testWidgets('a bill saved on credit lands in the outbox and nowhere else', (tester) async {
    final shop = await db.findCustomer('shop-1');
    await openBill(tester, shop!);
    await addPacket(tester);

    await tester.tap(find.text('Save'));
    await tester.pumpAndSettle();

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
  });

  testWidgets('a bill paid on the spot records the money too', (tester) async {
    final shop = await db.findCustomer('shop-1');
    await openBill(tester, shop!);
    await addPacket(tester);

    await tester.tap(find.text('Save'));
    await tester.pumpAndSettle();
    await tester.tap(find.textContaining('Paid ₹35.00 now'));
    await tester.pumpAndSettle();

    final entries = await db.select(db.outboxEntries).get();
    final payment = entries.firstWhere((e) => e.type == 'Payment');

    expect(jsonDecode(payment.payload)['payment']['amount'], 35.0);
  });

  testWidgets('part payment records what was actually handed over', (tester) async {
    final shop = await db.findCustomer('shop-1');
    await openBill(tester, shop!);
    await addPacket(tester);

    await tester.tap(find.text('Save'));
    await tester.pumpAndSettle();
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
    final shop = await db.findCustomer('shop-1');

    await openBill(tester, shop!);

    Future<void> writeOne() async {
      await addPacket(tester);
      await tester.tap(find.text('Save'));
      await tester.pumpAndSettle();
      await tester.tap(find.text('On credit'));
      await tester.pumpAndSettle();

      // Saving pops back to the host screen, exactly as it does in the app.
      await tester.tap(find.text('open bill'));
      await tester.pumpAndSettle();
    }

    await writeOne();
    await writeOne();

    final ids = (await db.select(db.outboxEntries).get()).map((e) => e.clientRequestId).toSet();

    // Two bills, two visits, four ids. The server tells them apart by these and by nothing else.
    expect(ids, hasLength(4));
  });
}
