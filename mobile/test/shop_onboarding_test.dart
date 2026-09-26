import 'dart:convert';

import 'package:drift/drift.dart' hide isNotNull, isNull;
import 'package:drift/native.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:golden_pappadam_sales/app.dart';
import 'package:golden_pappadam_sales/data/local/database.dart';
import 'package:golden_pappadam_sales/data/sales_repository.dart';
import 'package:golden_pappadam_sales/features/shops/shop_screen.dart';
import 'package:golden_pappadam_sales/features/shops/shops_screen.dart';

/// A salesman finds a shop on the road: he adds it with its rates - checked against the shops he
/// already has - and can bill it with no signal. He can also change its rates, as product cards,
/// and add a branch from the shop's page.
void main() {
  late AppDatabase db;

  setUp(() async {
    db = AppDatabase(NativeDatabase.memory());
    await db.replaceSnapshot(
      customers: [
        shopRow(id: 'kumar', name: 'Kumar Stores', balance: 0),
        shopRow(id: 'danya', name: 'Danya Supermarket', balance: 0, hasMultipleBranches: true),
      ],
      products: [
        ProductsCompanion.insert(
            id: 'p1', productCode: 'PKT-20', name: '20 piece packet', unitCode: 'PKT', defaultPrice: const Value(45)),
        ProductsCompanion.insert(
            id: 'p2', productCode: 'PKT-6', name: '6 piece packet', unitCode: 'PKT', defaultPrice: const Value(15)),
      ],
      prices: [CustomerPricesCompanion.insert(customerId: 'kumar', productId: 'p1', unitPrice: 35)],
    );
  });

  tearDown(() => db.close());

  /// These pages watch the outbox, so they never settle by themselves: let the database answer,
  /// then draw, a few times over.
  Future<void> settle(WidgetTester tester) async {
    for (var i = 0; i < 6; i++) {
      await tester.runAsync(() => Future<void>.delayed(const Duration(milliseconds: 40)));
      await tester.pump(const Duration(milliseconds: 300));
    }
  }

  Future<void> open(WidgetTester tester, Widget screen) async {
    tester.view.physicalSize = const Size(1080, 2400);
    tester.view.devicePixelRatio = 2.5;
    addTearDown(tester.view.reset);

    await tester.pumpWidget(ProviderScope(
      overrides: [databaseProvider.overrideWithValue(db)],
      child: MaterialApp(home: screen),
    ));
    await settle(tester);
  }

  /// Takes the pages away before the database closes, so the watched queries' timers finish here.
  Future<void> unmount(WidgetTester tester) async {
    await tester.pumpWidget(const SizedBox.shrink());
    await tester.pump(const Duration(seconds: 1));
  }

  Future<void> tap(WidgetTester tester, Finder finder) async {
    await tester.ensureVisible(finder);
    await tester.tap(finder);
    await settle(tester);
  }

  Future<void> type(WidgetTester tester, Finder finder, String text) async {
    await tester.ensureVisible(finder);
    await tester.enterText(finder, text);
    await settle(tester);
  }

  /// Picks a product on one of the rate cards, as the salesman would.
  Future<void> chooseRateProduct(WidgetTester tester, String name, {int index = 0}) async {
    final field = find.descendant(
      of: find.descendant(
          of: find.byKey(ValueKey('rate-line-$index')), matching: find.byType(DropdownMenu<CachedProduct>)),
      matching: find.byType(TextField),
    );
    await tap(tester, field);
    await tap(tester, find.text(name).last);
  }

  /// The outbox as the sync engine will send it.
  Future<List<OutboxEntry>> outbox(WidgetTester tester) async =>
      (await tester.runAsync(() => db.dueEntries(DateTime.now().toUtc(), ignoreBackoff: true)))!;

  FilledButton saveShop(WidgetTester tester) =>
      tester.widget<FilledButton>(find.byKey(const ValueKey('save-shop')));

  testWidgets('New shop is always on the Shops screen, and a shop already known cannot be added twice',
      (tester) async {
    await open(tester, const ShopsScreen());

    await tap(tester, find.byKey(const ValueKey('new-shop')));
    await type(tester, find.byKey(const ValueKey('shop-name')), 'kumar  STORES');

    expect(find.text('Kumar Stores is already a shop.'), findsOneWidget);
    expect(find.text('Open Kumar Stores'), findsOneWidget);
    expect(saveShop(tester).onPressed, isNull);

    await unmount(tester);
  });

  testWidgets('another shop of a known customer is pointed at that customer, in the search and the form',
      (tester) async {
    await open(tester, const ShopsScreen());

    await type(tester, find.byType(TextField).first, 'Danya Supermarket Coimbatore');
    expect(find.text('Is this a branch of Danya Supermarket?'), findsOneWidget);

    // The button carries the search over, and the form says the same thing.
    await tap(tester, find.byKey(const ValueKey('new-shop')));
    expect(find.byKey(const ValueKey('parent-shop')), findsOneWidget);

    await unmount(tester);
  });

  testWidgets('a shop found on the road is added with its rates on product cards and opens ready to bill',
      (tester) async {
    await open(tester, const ShopsScreen());

    await tap(tester, find.byKey(const ValueKey('new-shop')));
    await type(tester, find.byKey(const ValueKey('shop-name')), 'Anand Bakery');
    await type(tester, find.byKey(const ValueKey('shop-phone')), '9847012345');

    await chooseRateProduct(tester, '20 piece packet');
    await type(tester, find.byKey(const ValueKey('rate-value-0')), '38');
    await tap(tester, find.byKey(const ValueKey('rate-add-item')));
    await chooseRateProduct(tester, '6 piece packet', index: 1);
    await type(tester, find.byKey(const ValueKey('rate-value-1')), '14');

    await tap(tester, find.byKey(const ValueKey('save-shop')));

    // Straight onto the new shop's page, with the agreed rates showing.
    expect(find.text('Anand Bakery'), findsWidgets);
    expect(find.text('₹38.00'), findsOneWidget);
    expect(find.text('₹14.00'), findsOneWidget);

    final entries = await outbox(tester);
    expect(entries.map((e) => e.type), ['Customer', 'CustomerPrice', 'CustomerPrice']);
    expect((jsonDecode(entries.first.payload)['customer'] as Map)['phone'], '9847012345');

    await unmount(tester);
  });

  testWidgets('a rate is changed on a product card from the shop page, and applies from the next bill',
      (tester) async {
    await open(tester, const ShopScreen(shopId: 'kumar'));

    // Tapping the shop's rate opens the rates screen with that product already on the card.
    await tap(tester, find.byKey(const ValueKey('rate-row-p1')));
    expect(find.text('Now ₹35.00'), findsOneWidget);

    await type(tester, find.byKey(const ValueKey('rate-value-0')), '36');
    await tap(tester, find.byKey(const ValueKey('save-rates')));

    expect(find.text('₹36.00'), findsOneWidget);
    expect((await tester.runAsync(() => db.pricesFor('kumar')))!['p1'], 36);

    final entry = (await outbox(tester)).single;
    expect(entry.type, 'CustomerPrice');
    expect(jsonDecode(entry.payload)['customerPrice'], {'customerId': 'kumar', 'productId': 'p1', 'unitPrice': 36.0});

    await unmount(tester);
  });

  testWidgets('a product already on a rate card is not offered on another', (tester) async {
    await open(tester, const ShopScreen(shopId: 'kumar'));

    await tap(tester, find.byKey(const ValueKey('set-rate')));
    await chooseRateProduct(tester, '20 piece packet');
    await tap(tester, find.byKey(const ValueKey('rate-add-item')));

    await tap(
      tester,
      find.descendant(
        of: find.descendant(of: find.byKey(const ValueKey('rate-line-1')), matching: find.byType(DropdownMenu<CachedProduct>)),
        matching: find.byType(TextField),
      ),
    );
    final menu = find.descendant(of: find.byKey(const ValueKey('rate-line-1')), matching: find.byType(MenuItemButton));
    expect(find.descendant(of: menu, matching: find.text('20 piece packet')), findsNothing);
    expect(find.descendant(of: menu, matching: find.text('6 piece packet')), findsOneWidget);

    await unmount(tester);
  });

  testWidgets('a branch is added under its shop, never as a new customer', (tester) async {
    await open(tester, const ShopScreen(shopId: 'danya'));

    expect(find.text('No branches yet. Add one before billing this shop.'), findsOneWidget);

    await tap(tester, find.byKey(const ValueKey('add-branch')));
    await type(tester, find.byKey(const ValueKey('branch-name')), 'Coimbatore');
    await tap(tester, find.byKey(const ValueKey('save-branch')));

    expect(find.text('Coimbatore'), findsOneWidget);

    final entry = (await outbox(tester)).single;
    expect(entry.type, 'CustomerBranch');
    expect((jsonDecode(entry.payload)['branch'] as Map)['customerId'], 'danya');
    expect((await tester.runAsync(() => db.allCustomers()))!, hasLength(2));

    await unmount(tester);
  });
}
