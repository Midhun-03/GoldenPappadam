import 'dart:convert';

import 'package:drift/native.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:golden_pappadam_sales/app.dart';
import 'package:golden_pappadam_sales/core/theme.dart';
import 'package:golden_pappadam_sales/data/local/database.dart';
import 'package:golden_pappadam_sales/data/remote/api_client.dart';
import 'package:golden_pappadam_sales/data/sales_repository.dart';
import 'package:golden_pappadam_sales/features/home/dashboard_screen.dart';
import 'package:golden_pappadam_sales/features/sale/sale_screen.dart';
import 'package:golden_pappadam_sales/features/sync/sync_views.dart';
import 'package:golden_pappadam_sales/sync/sync_engine.dart';

/// The office, answering the one live question Home asks: what is on the van.
class FakeApi implements ApiClient {
  Object Function()? vanStock;

  @override
  Future<Map<String, dynamic>> get(String path, {Map<String, dynamic>? query}) async {
    if (path.endsWith('/van-stock') && vanStock != null) {
      final answer = vanStock!();
      if (answer is Map<String, dynamic>) return answer;
      throw answer;
    }

    return <String, dynamic>{};
  }

  @override
  noSuchMethod(Invocation invocation) => super.noSuchMethod(invocation);
}

/// Home shows what the phone already holds - the day summary cached at the last sync, the shops,
/// the outbox - and every figure and action it had before the redesign is still there.
void main() {
  late AppDatabase db;
  late FakeApi api;
  late SyncEngine engine;

  setUp(() async {
    db = AppDatabase(NativeDatabase.memory());
    api = FakeApi()
      ..vanStock = () => <String, dynamic>{
            'lines': [
              {'loaded': 300, 'sold': 120, 'unaccounted': 180},
              {'loaded': 50, 'sold': 20, 'unaccounted': 30},
            ],
          };
    engine = SyncEngine(database: db, apiClient: api);

    await db.replaceSnapshot(
      customers: [
        shopRow(id: 'shop-1', name: 'Kumar Stores', balance: 1000),
        shopRow(id: 'shop-2', name: 'Anand Bakery', balance: 0),
      ],
      products: const [],
      prices: const [],
    );
  });

  tearDown(() async {
    await engine.dispose();
    await db.close();
  });

  Future<void> cacheDay() => db.writeMeta(
        'day_summary',
        jsonEncode({
          'totalSales': 12450.5,
          'saleCount': 3,
          'shopsVisited': 1,
          'cashCollected': 4200,
          'creditSales': 8250.5,
        }),
      );

  Future<void> settle(WidgetTester tester) async {
    for (var round = 0; round < 6; round++) {
      await tester.runAsync(() => Future<void>.delayed(const Duration(milliseconds: 50)));
      await tester.pump(const Duration(milliseconds: 300));
    }
  }

  Future<void> openHome(WidgetTester tester) async {
    // A phone-sized surface, so the whole of Home is built rather than the 800x600 default.
    tester.view.physicalSize = const Size(1080, 4200);
    tester.view.devicePixelRatio = 3;
    addTearDown(tester.view.reset);

    await tester.pumpWidget(ProviderScope(
      overrides: [
        databaseProvider.overrideWithValue(db),
        syncProvider.overrideWithValue(engine),
      ],
      child: MaterialApp(theme: AppTheme.light(), home: const DashboardScreen()),
    ));
    await settle(tester);
  }

  Future<void> unmount(WidgetTester tester) async {
    await tester.pumpWidget(const SizedBox.shrink());
    await tester.pump(const Duration(seconds: 1));
  }

  testWidgets('shows the cached day, the shops, the van and what is waiting', (tester) async {
    await tester.runAsync(() async {
      await cacheDay();
      await db.enqueue(
        clientRequestId: 'sale-1',
        type: 'Invoice',
        payload: const {},
        recordedAt: DateTime.now().toUtc(),
        summary: 'Kumar Stores · 350',
      );
    });

    await openHome(tester);

    expect(find.text('₹12,450.50'), findsOneWidget);
    expect(find.text('3 deliveries'), findsOneWidget);
    expect(find.text('₹4,200.00'), findsOneWidget);
    expect(find.text('₹8,250.50'), findsOneWidget);

    // One shop of the two the phone knows.
    expect(find.text('1 / 2', findRichText: true), findsOneWidget);

    // The van, added up across its lines.
    expect(find.text('350'), findsOneWidget);
    expect(find.text('140'), findsOneWidget);
    expect(find.text('210'), findsOneWidget);

    // The unsent bill is counted in the corner, explained on the figures it is missing from, and
    // listed.
    expect(find.text('1 pending'), findsOneWidget);
    expect(find.text('1 waiting to sync'), findsOneWidget);
    expect(find.text("TODAY'S BILLS"), findsOneWidget);
    expect(find.text('Kumar Stores · 350'), findsOneWidget);

    await unmount(tester);
  });

  testWidgets('with nothing waiting it says so, and how old the figures are', (tester) async {
    await tester.runAsync(() async {
      await cacheDay();
      await db.writeMeta('last_synced_at', DateTime.now().toUtc().toIso8601String());
    });

    await openHome(tester);

    expect(find.text('Synced'), findsOneWidget);
    expect(find.text('Office figures · updated just now'), findsOneWidget);
    expect(find.textContaining('waiting to sync'), findsNothing);

    await unmount(tester);
  });

  testWidgets('before the first sync it says there is no summary and still offers both actions',
      (tester) async {
    api.vanStock = () => OfflineException();

    await openHome(tester);

    expect(find.text('No summary yet'), findsOneWidget);
    expect(find.text('No connection. Open Van for the last figures.'), findsOneWidget);
    expect(find.text('New Bill'), findsOneWidget);
    expect(find.text('Receive Payment'), findsOneWidget);

    await unmount(tester);
  });

  testWidgets('New Bill opens the bill page', (tester) async {
    await openHome(tester);

    await tester.tap(find.text('New Bill'));
    await settle(tester);

    expect(find.byType(SaleScreen), findsOneWidget);

    await unmount(tester);
  });

  testWidgets('Receive Payment asks which shop first', (tester) async {
    await openHome(tester);

    await tester.tap(find.text('Receive Payment'));
    await settle(tester);

    expect(find.text('Choose a shop'), findsOneWidget);
    expect(find.text('Kumar Stores'), findsOneWidget);
    expect(find.text('Owes ₹1,000.00'), findsOneWidget);

    await unmount(tester);
  });

  testWidgets('the sync pill opens what is waiting to go up', (tester) async {
    await openHome(tester);

    await tester.tap(find.text('Synced'));
    await settle(tester);

    expect(find.byType(PendingScreen), findsOneWidget);

    await unmount(tester);
  });
}
