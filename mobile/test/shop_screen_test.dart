import 'package:drift/native.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:golden_pappadam_sales/app.dart';
import 'package:golden_pappadam_sales/data/local/database.dart';
import 'package:golden_pappadam_sales/data/sales_repository.dart';
import 'package:golden_pappadam_sales/features/shops/shop_screen.dart';

void main() {
  late AppDatabase db;

  setUp(() async {
    db = AppDatabase(NativeDatabase.memory());
    await db.replaceSnapshot(
      customers: [shopRow(id: 'shop-1', name: 'Kumar Stores', balance: 1000)],
      products: const [],
      prices: const [],
    );
  });

  tearDown(() => db.close());

  Future<void> openShop(WidgetTester tester) async {
    await tester.pumpWidget(ProviderScope(
      overrides: [databaseProvider.overrideWithValue(db)],
      child: const MaterialApp(home: ShopScreen(shopId: 'shop-1')),
    ));

    // The page listens to the outbox, so it never settles on its own: pump, then let the
    // database's futures finish.
    for (var i = 0; i < 5; i++) {
      await tester.runAsync(() => Future<void>.delayed(const Duration(milliseconds: 50)));
      await tester.pump();
    }
  }

  /// Takes the page away before the database closes, so the watched query's clean-up timer runs
  /// inside the test instead of outliving it.
  Future<void> unmount(WidgetTester tester) async {
    await tester.pumpWidget(const SizedBox.shrink());
    await tester.pump(const Duration(seconds: 1));
  }

  testWidgets('money just taken shows in the payment history as waiting, with its time', (tester) async {
    await tester.runAsync(() => SalesRepository(db).recordPayment(
          customerId: 'shop-1',
          customerName: 'Kumar Stores',
          amount: 500,
          method: 'Cash',
        ));

    await openShop(tester);

    // The history once looked for the literal text "$shopName" and never found this payment.
    expect(find.text('PAYMENT HISTORY'), findsOneWidget);
    expect(find.textContaining('Waiting to go up · '), findsOneWidget);
    expect(find.textContaining(r'${'), findsNothing);

    await unmount(tester);
  });

  testWidgets('the shop page offers to record expired or damaged packets', (tester) async {
    await openShop(tester);

    expect(find.text('Expired or damaged packets'), findsOneWidget);

    await unmount(tester);
  });
}
