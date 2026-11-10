import 'dart:convert';

import 'package:drift/native.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:golden_pappadam_sales/app.dart';
import 'package:golden_pappadam_sales/data/local/database.dart';
import 'package:golden_pappadam_sales/data/sales_repository.dart';
import 'package:golden_pappadam_sales/features/payment/payment_screen.dart';

/// Collecting money from a shop: what is typed and chosen on the screen is what lands in the
/// outbox, and nothing can be recorded until there is an amount.
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

  Finder collect() => find.widgetWithText(FilledButton, 'Collect Payment');

  Future<void> openPayment(WidgetTester tester) async {
    tester.view.physicalSize = const Size(1080, 2400);
    tester.view.devicePixelRatio = 2.5;
    addTearDown(tester.view.reset);

    final shop = (await db.findCustomer('shop-1'))!;

    await tester.pumpWidget(ProviderScope(
      overrides: [databaseProvider.overrideWithValue(db)],
      child: MaterialApp(
        home: Builder(
          builder: (context) => Scaffold(
            body: Center(
              child: ElevatedButton(
                onPressed: () => Navigator.of(context).push(
                  MaterialPageRoute<void>(builder: (_) => PaymentScreen(shop: shop)),
                ),
                child: const Text('open payment'),
              ),
            ),
          ),
        ),
      ),
    ));

    await tester.tap(find.text('open payment'));
    await tester.pumpAndSettle();
  }

  Future<Map<String, dynamic>> savedPayment() async {
    final entry = (await db.select(db.outboxEntries).get()).singleWhere((e) => e.type == 'Payment');

    return Map<String, dynamic>.from(jsonDecode(entry.payload)['payment'] as Map);
  }

  testWidgets('shows what the shop owes and cannot record nothing', (tester) async {
    await openPayment(tester);

    expect(find.text('Kumar Stores'), findsOneWidget);
    expect(find.text('₹1,000.00'), findsOneWidget);
    expect(tester.widget<FilledButton>(collect()).onPressed, isNull);
  });

  testWidgets('a part payment shows what will be left, and saves what was typed', (tester) async {
    await openPayment(tester);

    await tester.enterText(find.byKey(const ValueKey('payment-amount')), '400');
    await tester.pumpAndSettle();

    expect(find.text('Left after this payment'), findsOneWidget);
    expect(find.text('₹600.00'), findsOneWidget);

    await tester.tap(find.text('UPI'));
    await tester.pumpAndSettle();
    await tester.tap(collect());
    await tester.pumpAndSettle();

    final payment = await savedPayment();
    expect(payment['amount'], 400.0);
    expect(payment['method'], 'UPI');

    // Saving goes back to where the payment was opened from.
    expect(find.text('open payment'), findsOneWidget);
  });

  testWidgets('the whole balance is one tap', (tester) async {
    await openPayment(tester);

    await tester.tap(find.text('The whole ₹1,000.00'));
    await tester.pumpAndSettle();
    await tester.tap(collect());
    await tester.pumpAndSettle();

    final payment = await savedPayment();
    expect(payment['amount'], 1000.0);
    expect(payment['method'], 'Cash');
  });

  testWidgets('more than is owed is allowed, and says where the rest goes', (tester) async {
    await openPayment(tester);

    await tester.enterText(find.byKey(const ValueKey('payment-amount')), '1500');
    await tester.pumpAndSettle();

    expect(find.textContaining('kept on account'), findsOneWidget);
    expect(find.text('Left after this payment'), findsNothing);
    expect(tester.widget<FilledButton>(collect()).onPressed, isNotNull);
  });
}
