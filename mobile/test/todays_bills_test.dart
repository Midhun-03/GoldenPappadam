import 'package:drift/native.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:golden_pappadam_sales/app.dart';
import 'package:golden_pappadam_sales/data/local/database.dart';
import 'package:golden_pappadam_sales/features/home/dashboard_screen.dart';

/// Home's list of the day's bills: a bill has no number until the office gives it one, and then it
/// shows that number - the one printed on the invoice.
void main() {
  late AppDatabase db;

  setUp(() => db = AppDatabase(NativeDatabase.memory()));
  tearDown(() => db.close());

  Future<void> sale(String id, String summary, {DateTime? at}) => db.enqueue(
        clientRequestId: id,
        type: 'Invoice',
        payload: const {},
        recordedAt: at ?? DateTime.now().toUtc(),
        summary: summary,
      );

  /// Drift delivers a watched query asynchronously, so the first frames are pumped with real time
  /// for the query to answer. [unmount] takes the widget away again before the database closes, so
  /// the stream's own clean-up timer runs inside the test instead of outliving it.
  Future<void> pump(WidgetTester tester) async {
    await tester.pumpWidget(ProviderScope(
      overrides: [databaseProvider.overrideWithValue(db)],
      child: const MaterialApp(home: Scaffold(body: TodaysBills())),
    ));
    // A few rounds, because the very first query in a run also pays for loading SQLite.
    for (var round = 0; round < 5; round++) {
      await tester.runAsync(() => Future<void>.delayed(const Duration(milliseconds: 50)));
      await tester.pump();
    }
  }

  Future<void> unmount(WidgetTester tester) async {
    await tester.pumpWidget(const SizedBox.shrink());
    await tester.pump(const Duration(seconds: 1));
  }

  testWidgets('a synced bill shows its official number and an unsent one says it is waiting', (tester) async {
    await tester.runAsync(() async {
      await sale('sale-1', 'Kumar Stores · 350');
      await sale('sale-2', 'Anand Bakery · 120');
      await db.markSynced('sale-1', 'server-1', documentNumber: 'GP/26-27/000125');
    });

    await pump(tester);

    // SectionHeader shows its label in capitals.
    expect(find.text("TODAY'S BILLS"), findsOneWidget);
    expect(find.textContaining('GP/26-27/000125'), findsOneWidget);
    expect(find.textContaining('Waiting to sync'), findsOneWidget);
    await unmount(tester);
  });

  testWidgets('a refused bill says why', (tester) async {
    await tester.runAsync(() async {
      await sale('sale-1', 'Kumar Stores · 350');
      await db.markFailed('sale-1', 'Kumar Stores is a GST customer.');
    });

    await pump(tester);

    expect(find.text('Kumar Stores is a GST customer.'), findsOneWidget);
    await unmount(tester);
  });

  testWidgets('yesterday\'s bills and an empty day show nothing', (tester) async {
    await tester.runAsync(() => sale('old', 'Kumar Stores · 350',
        at: DateTime.now().toUtc().subtract(const Duration(days: 2))));

    await pump(tester);

    expect(find.text("TODAY'S BILLS"), findsNothing);
    expect(find.byType(ListTile), findsNothing);
    await unmount(tester);
  });
}
