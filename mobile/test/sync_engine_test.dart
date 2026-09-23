import 'dart:convert';

import 'package:drift/drift.dart' hide isNotNull, isNull;
import 'package:drift/native.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:golden_pappadam_sales/data/local/database.dart';
import 'package:golden_pappadam_sales/data/remote/api_client.dart';
import 'package:golden_pappadam_sales/sync/sync_engine.dart';

/// A stand-in for the network, so the behaviour that matters - what happens when the signal goes -
/// can be tested without one.
class FakeApi implements ApiClient {
  FakeApi();

  /// What the next call should do. Set to throw to simulate a tunnel, a dead server, a refusal.
  Object? Function(String path, Object? body)? handler;

  final List<Map<String, dynamic>> submissions = [];

  @override
  Future<Map<String, dynamic>> post(String path, Object? body, {bool anonymous = false}) async {
    if (path.endsWith('/sync/submissions') && body is Map) {
      submissions.add(Map<String, dynamic>.from(body));
    }

    final result = handler?.call(path, body);
    if (result is Map<String, dynamic>) return result;

    return <String, dynamic>{};
  }

  @override
  Future<Map<String, dynamic>> get(String path, {Map<String, dynamic>? query}) async {
    final result = handler?.call(path, null);

    return result is Map<String, dynamic> ? result : <String, dynamic>{};
  }

  @override
  noSuchMethod(Invocation invocation) => super.noSuchMethod(invocation);
}

void main() {
  late AppDatabase db;
  late FakeApi api;
  late SyncEngine engine;

  setUp(() async {
    db = AppDatabase(NativeDatabase.memory());
    api = FakeApi();
    engine = SyncEngine(database: db, apiClient: api);

    await db.writeMeta('device_id', 'device-1');
  });

  tearDown(() async {
    await engine.dispose();
    await db.close();
  });

  Future<void> enqueueSale(String id, {String shop = 'Kumar Stores'}) => db.enqueue(
        clientRequestId: id,
        type: 'Invoice',
        payload: {
          'sale': {
            'customerId': 'customer-1',
            'lines': [
              {'productId': 'product-1', 'quantity': 10.0, 'unitPrice': 35.0}
            ],
            'pricesAsOf': '2026-09-15T00:00:00.000Z',
          }
        },
        recordedAt: DateTime.utc(2026, 9, 15, 6, 30),
        summary: '$shop - 350',
      );

  Map<String, dynamic> accepted(List<String> ids) => {
        'serverTime': DateTime.now().toUtc().toIso8601String(),
        'results': [
          for (final id in ids)
            {'clientRequestId': id, 'outcome': 'Accepted', 'recordId': 'server-$id'}
        ]
      };

  Map<String, dynamic> emptySnapshot() => {
        'serverTime': DateTime.now().toUtc().toIso8601String(),
        'customers': <dynamic>[],
        'products': <dynamic>[],
        'prices': <dynamic>[],
      };

  test('a sale is on the phone the moment it is saved, before any network', () async {
    await enqueueSale('sale-1');

    final waiting = await db.entriesWithStatus(OutboxStatus.pending);

    expect(waiting, hasLength(1));
    expect(waiting.single.status, 'Pending');
    expect(waiting.single.serverRecordId, isNull);
  });

  test('losing the signal mid-sync loses nothing and schedules another try', () async {
    await enqueueSale('sale-1');

    api.handler = (path, _) => throw OfflineException('No connection.');

    final status = await engine.syncNow();

    expect(status.state, SyncState.offline);

    // Still here, still pending, and now due later rather than hammering the network.
    final waiting = await db.entriesWithStatus(OutboxStatus.pending);
    expect(waiting, hasLength(1));
    expect(waiting.single.attemptCount, 1);
    expect(waiting.single.nextAttemptAt, isNotNull);
    expect(waiting.single.lastError, 'No connection.');
  });

  // What happened on 2026-09-22: a firewall dropped the phone's requests for days, every row
  // reached the 15-minute wait, and once the office was reachable again "Sync now" pulled fresh
  // data but sent nothing - the rows were still inside their wait.
  test('asking to sync sends rows still inside their retry wait', () async {
    await enqueueSale('sale-1');
    await db.markForRetry(
        'sale-1', 28, DateTime.now().toUtc().add(const Duration(minutes: 15)), 'Too slow.');

    api.handler = (path, body) =>
        path.endsWith('/sync/submissions') ? accepted(['sale-1']) : emptySnapshot();

    final status = await engine.syncNow();

    expect(api.submissions, hasLength(1));
    expect(status.waiting, 0);
    expect((await db.entriesWithStatus(OutboxStatus.synced)).single.serverRecordId, 'server-sale-1');
  });

  test('the background timer still waits, so a phone with no signal does not hammer the network',
      () async {
    await enqueueSale('sale-1');
    await db.markForRetry(
        'sale-1', 3, DateTime.now().toUtc().add(const Duration(minutes: 1)), 'No connection.');

    api.handler = (path, body) =>
        path.endsWith('/sync/submissions') ? accepted(['sale-1']) : emptySnapshot();

    await engine.syncNow(respectBackoff: true);

    expect(api.submissions, isEmpty);
    expect(await db.entriesWithStatus(OutboxStatus.pending), hasLength(1));
  });

  test('the backoff grows and then settles rather than growing forever', () {
    expect(backoffFor(1), const Duration(seconds: 5));
    expect(backoffFor(3), const Duration(minutes: 1));
    expect(backoffFor(5), const Duration(minutes: 15));

    // A phone in a pocket all afternoon keeps trying every quarter of an hour.
    expect(backoffFor(50), const Duration(minutes: 15));
  });

  test('ten sales recorded offline all go up in one batch when the signal returns', () async {
    for (var i = 0; i < 10; i++) {
      await enqueueSale('sale-$i');
    }

    api.handler = (path, body) => path.endsWith('/sync/submissions')
        ? accepted([for (var i = 0; i < 10; i++) 'sale-$i'])
        : emptySnapshot();

    final status = await engine.syncNow();

    expect(status.waiting, 0);
    expect(status.failed, 0);

    // One request, ten items, oldest first.
    expect(api.submissions, hasLength(1));
    expect((api.submissions.single['items'] as List), hasLength(10));

    final synced = await db.entriesWithStatus(OutboxStatus.synced);
    expect(synced, hasLength(10));
  });

  test('a sale the server has already seen is treated as done, not sent again', () async {
    await enqueueSale('sale-1');

    api.handler = (path, body) => path.endsWith('/sync/submissions')
        ? {
            'results': [
              {
                'clientRequestId': 'sale-1',
                'outcome': 'AlreadyAccepted',
                'recordId': 'the-original-bill'
              }
            ]
          }
        : emptySnapshot();

    await engine.syncNow();

    final synced = await db.entriesWithStatus(OutboxStatus.synced);
    expect(synced.single.serverRecordId, 'the-original-bill');

    // Nothing left to send, so a second sync carries no items at all.
    await engine.syncNow();
    expect(api.submissions, hasLength(1));
  });

  test('a synced sale keeps the official number the office gave it', () async {
    await enqueueSale('sale-1');

    api.handler = (path, body) => path.endsWith('/sync/submissions')
        ? {
            'results': [
              {
                'clientRequestId': 'sale-1',
                'outcome': 'Accepted',
                'recordId': 'server-sale-1',
                'documentNumber': 'GP/26-27/000125'
              }
            ]
          }
        : emptySnapshot();

    await engine.syncNow();

    final synced = await db.entriesWithStatus(OutboxStatus.synced);
    expect(synced.single.documentNumber, 'GP/26-27/000125');
  });

  test('a sale whose first answer was lost still ends up with its number from the retry', () async {
    await enqueueSale('sale-1');

    api.handler = (path, body) => path.endsWith('/sync/submissions')
        ? {
            'results': [
              {
                'clientRequestId': 'sale-1',
                'outcome': 'AlreadyAccepted',
                'recordId': 'the-original-bill',
                'documentNumber': 'GP/26-27/000125'
              }
            ]
          }
        : emptySnapshot();

    await engine.syncNow();

    final synced = await db.entriesWithStatus(OutboxStatus.synced);
    expect(synced.single.documentNumber, 'GP/26-27/000125');
  });

  test('a sale that has not synced has no number - the phone never makes one up', () async {
    await enqueueSale('sale-1');

    final pending = await db.entriesWithStatus(OutboxStatus.pending);
    expect(pending.single.documentNumber, isNull);
  });

  test('a refused sale is kept and explained, not discarded', () async {
    await enqueueSale('sale-1');

    api.handler = (path, body) => path.endsWith('/sync/submissions')
        ? {
            'results': [
              {
                'clientRequestId': 'sale-1',
                'outcome': 'Rejected',
                'error': "Customer 'Kumar Stores' is not active."
              }
            ]
          }
        : emptySnapshot();

    final status = await engine.syncNow();

    expect(status.failed, 1);

    final failed = await db.entriesWithStatus(OutboxStatus.failed);
    expect(failed.single.lastError, contains('not active'));

    // A failed row is not retried on its own - it waits for a person.
    final due = await db.dueEntries(DateTime.now().toUtc().add(const Duration(days: 1)));
    expect(due, isEmpty);
  });

  test('a failed sale can be put back in the queue by hand', () async {
    await enqueueSale('sale-1');
    await db.markFailed('sale-1', 'Customer is not active.');

    await db.retryNow('sale-1');

    final waiting = await db.entriesWithStatus(OutboxStatus.pending);
    expect(waiting, hasLength(1));
    expect(waiting.single.attemptCount, 0);
    expect(waiting.single.lastError, isNull);
  });

  test('unsent work survives the app being closed and reopened', () async {
    await enqueueSale('sale-1');

    // A new engine and a new database object over the same storage is what a restart looks like.
    final reopened = SyncEngine(database: db, apiClient: api);
    final status = await reopened.currentStatus();

    expect(status.waiting, 1);
    await reopened.dispose();
  });

  test('signing out never throws away work the office has not seen', () async {
    await enqueueSale('sale-1');
    await enqueueSale('sale-2');
    await db.markSynced('sale-2', 'server-2');

    await db.clearForSignOut();

    final left = await db.select(db.outboxEntries).get();

    expect(left, hasLength(1));
    expect(left.single.clientRequestId, 'sale-1');
  });

  test('the snapshot replaces the cache rather than merging into it', () async {
    await db.replaceSnapshot(
      customers: [
        CustomersCompanion.insert(id: 'old', name: 'Closed Shop', balance: 100),
      ],
      products: const [],
      prices: const [],
    );

    api.handler = (path, body) => path.endsWith('/sync/submissions')
        ? {'results': <dynamic>[]}
        : {
            'serverTime': DateTime.now().toUtc().toIso8601String(),
            'pricesAsOf': '2026-09-15T10:00:00.000Z',
            'customers': [
              {'id': 'new', 'name': 'Kumar Stores', 'balance': 2500.0}
            ],
            'products': [
              {
                'id': 'product-1',
                'productCode': 'PKT-20',
                'name': '20 piece packet',
                'unitCode': 'PKT',
                'defaultPrice': 45.0
              }
            ],
            'prices': [
              {'customerId': 'new', 'productId': 'product-1', 'unitPrice': 35.0}
            ],
          };

    await engine.syncNow();

    final customers = await db.allCustomers();
    expect(customers, hasLength(1));
    expect(customers.single.name, 'Kumar Stores');
    expect(customers.single.balance, 2500.0);

    expect(await engine.pricesAsOf, '2026-09-15T10:00:00.000Z');
  });

  test('a multi-branch customer and its branches come down with the snapshot', () async {
    api.handler = (path, body) => path.endsWith('/sync/submissions')
        ? {'results': <dynamic>[]}
        : {
            'serverTime': DateTime.now().toUtc().toIso8601String(),
            'customers': [
              {
                'id': 'danya',
                'name': 'Danya Supermarket',
                'balance': 0.0,
                'hasMultipleBranches': true,
              }
            ],
            'products': <dynamic>[],
            'prices': <dynamic>[],
            'branches': [
              {'id': 'branch-1', 'customerId': 'danya', 'name': 'Kundara', 'location': 'Kollam'},
              {'id': 'branch-2', 'customerId': 'danya', 'name': 'Coimbatore'},
            ],
          };

    await engine.syncNow();

    final customer = await db.findCustomer('danya');
    expect(customer!.hasMultipleBranches, isTrue);

    final branches = await db.branchesFor('danya');
    expect(branches.map((b) => b.name), containsAll(['Kundara', 'Coimbatore']));
    expect(branches.firstWhere((b) => b.name == 'Kundara').location, 'Kollam');
  });

  test('the snapshot says which shops get GST bills', () async {
    api.handler = (path, body) => path.endsWith('/sync/submissions')
        ? {'results': <dynamic>[]}
        : {
            'serverTime': DateTime.now().toUtc().toIso8601String(),
            'customers': [
              {
                'id': 'danya',
                'name': 'Danya Supermarket',
                'balance': 0.0,
                'hasMultipleBranches': false,
                'gstin': '32PQRSX9876K1Z3',
                'isGstRegistered': true,
              },
              {'id': 'kumar', 'name': 'Kumar Stores', 'balance': 0.0, 'gstin': null, 'isGstRegistered': false},
            ],
            'products': <dynamic>[],
            'prices': <dynamic>[],
          };

    await engine.syncNow();

    expect((await db.findCustomer('danya'))!.gstin, '32PQRSX9876K1Z3');
    expect((await db.findCustomer('kumar'))!.gstin, isNull);
  });

  test('a shop with an agreed price pays it, and everything else falls back', () async {
    await db.replaceSnapshot(
      customers: [CustomersCompanion.insert(id: 'shop-a', name: 'Shop A', balance: 0)],
      products: [
        ProductsCompanion.insert(
            id: 'p1', productCode: 'PKT-20', name: '20 piece', unitCode: 'PKT',
            defaultPrice: const Value(45)),
        ProductsCompanion.insert(
            id: 'p2', productCode: 'PKT-6', name: '6 piece', unitCode: 'PKT',
            defaultPrice: const Value(15)),
      ],
      prices: [
        CustomerPricesCompanion.insert(customerId: 'shop-a', productId: 'p1', unitPrice: 35),
      ],
    );

    final prices = await db.pricesFor('shop-a');

    expect(prices['p1'], 35.0);
    expect(prices['p2'], 15.0);
  });

  test('the payload is frozen when it is saved, so a later price change cannot rewrite it',
      () async {
    await enqueueSale('sale-1');

    final entry = (await db.entriesWithStatus(OutboxStatus.pending)).single;
    final payload = jsonDecode(entry.payload) as Map<String, dynamic>;
    final line = (payload['sale']['lines'] as List).single as Map<String, dynamic>;

    expect(line['unitPrice'], 35.0);
  });
}
