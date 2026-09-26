import 'dart:async';
import 'dart:convert';

import 'package:drift/drift.dart';

import '../core/config.dart';
import '../data/local/database.dart';
import '../data/remote/api_client.dart';

/// How long to wait after each failed attempt. The last value repeats, so a phone left in a
/// pocket all afternoon tries every quarter of an hour rather than every second.
const List<Duration> retryBackoff = [
  Duration(seconds: 5),
  Duration(seconds: 15),
  Duration(minutes: 1),
  Duration(minutes: 5),
  Duration(minutes: 15),
];

Duration backoffFor(int attemptCount) =>
    retryBackoff[attemptCount.clamp(1, retryBackoff.length) - 1];

enum SyncState { idle, syncing, offline }

class SyncStatus {
  const SyncStatus({
    required this.state,
    required this.waiting,
    required this.failed,
    this.lastSyncedAt,
    this.message,
  });

  final SyncState state;

  /// Saved on the phone, not yet confirmed by the server.
  final int waiting;

  /// Refused by the server. These need a person, not another attempt.
  final int failed;

  final DateTime? lastSyncedAt;
  final String? message;

  bool get isSettled => waiting == 0 && failed == 0;
}

/// Local truth to server, then server back to local cache.
///
/// One worker, one lock: a salesperson tapping "Sync now" while the timer is already running must
/// not cause the same row to be sent twice. Even if it did, the server's client request id would
/// catch it - but two layers cost nothing here.
class SyncEngine {
  SyncEngine({required AppDatabase database, required ApiClient apiClient})
      : _db = database,
        _api = apiClient;

  static const _deviceIdKey = 'device_id';
  static const _lastSyncKey = 'last_synced_at';
  static const _pricesAsOfKey = 'prices_as_of';
  static const _vanLocationKey = 'van_location_id';
  static const _dayKey = 'day_summary';

  final AppDatabase _db;
  final ApiClient _api;

  final _statusController = StreamController<SyncStatus>.broadcast();
  bool _running = false;
  Timer? _timer;

  Stream<SyncStatus> get status => _statusController.stream;

  bool get isRunning => _running;

  void start() {
    // Only the background timer honours the retry wait. It exists so a phone with no signal does
    // not hammer the network; it must not also stop a person who can see the signal is back.
    _timer ??= Timer.periodic(AppConfig.syncInterval, (_) => syncNow(respectBackoff: true));
    unawaited(syncNow());
  }

  void stop() {
    _timer?.cancel();
    _timer = null;
  }

  Future<void> dispose() async {
    stop();
    await _statusController.close();
  }

  /// Registers this handset. Idempotent on the server: the same name and user updates the row
  /// rather than making a second device.
  Future<String> registerDevice({required String name, required String platform}) async {
    final body = await _api.post('/api/mobile/devices/register', {
      'name': name,
      'platform': platform,
    });

    final deviceId = body['id'] as String;
    await _db.writeMeta(_deviceIdKey, deviceId);

    final location = body['locationId'];
    if (location is String) await _db.writeMeta(_vanLocationKey, location);

    return deviceId;
  }

  Future<String?> get deviceId => _db.readMeta(_deviceIdKey);

  /// The van the office assigned this phone to, or null. Without one nothing can come off a van.
  Future<String?> get vanLocationId => _db.readMeta(_vanLocationKey);

  /// The whole cycle. Safe to call at any time and from anywhere; it simply returns if a run is
  /// already in progress.
  ///
  /// By default every waiting row is sent, even one still inside its retry wait: "Sync now", a
  /// pull to refresh, opening the app and the signal coming back all mean "try now". Without this,
  /// a phone that failed many times while the office was unreachable kept its rows parked for up to
  /// fifteen minutes after the office came back, while the pull beside them succeeded - which looks
  /// exactly like "not syncing". Resending is safe: the server's client request id makes it a no-op.
  Future<SyncStatus> syncNow({bool respectBackoff = false}) async {
    if (_running) return _describe(SyncState.syncing);

    _running = true;
    _emit(await _describe(SyncState.syncing));

    try {
      // Push first: the office should see what was done before the phone asks for fresh balances,
      // otherwise the balances it gets back are already out of date.
      await _push(respectBackoff: respectBackoff);
      await _pull();
      await _pullDay();

      await _db.writeMeta(_lastSyncKey, DateTime.now().toUtc().toIso8601String());

      final status = await _describe(SyncState.idle);
      _emit(status);

      return status;
    } on OfflineException catch (error) {
      final status = await _describe(SyncState.offline, message: error.message);
      _emit(status);

      return status;
    } finally {
      _running = false;
    }
  }

  // ---------- local truth to the server ----------

  Future<void> _push({required bool respectBackoff}) async {
    final deviceId = await _db.readMeta(_deviceIdKey);
    if (deviceId == null) return;

    final due = await _db.dueEntries(DateTime.now().toUtc(), ignoreBackoff: !respectBackoff);
    if (due.isEmpty) return;

    final items = due
        .map((entry) => {
              'clientRequestId': entry.clientRequestId,
              'type': entry.type,
              'recordedAt': entry.recordedAt.toUtc().toIso8601String(),
              ...decodePayload(entry.payload),
            })
        .toList();

    final Map<String, dynamic> body;

    try {
      body = await _api.post('/api/mobile/sync/submissions', {
        'deviceId': deviceId,
        'items': items,
      });
    } on OfflineException catch (error) {
      // Nothing was confirmed, so nothing changes except when to try again. Not one row is lost.
      await _scheduleRetries(due, error.message);
      rethrow;
    } on ApiException catch (error) {
      // The batch itself was refused - a deactivated device, say. Still not the rows' fault.
      await _scheduleRetries(due, error.message);
      return;
    }

    await _applyResults(body['results'] as List<dynamic>? ?? const []);
  }

  Future<void> _applyResults(List<dynamic> results) async {
    for (final result in results.whereType<Map<String, dynamic>>()) {
      final clientRequestId = result['clientRequestId'] as String;
      final outcome = result['outcome'] as String?;
      final recordId = result['recordId'] as String?;

      switch (outcome) {
        // Accepted and AlreadyAccepted mean the same thing to the phone: the office has it.
        // Treating a repeat as success is the entire point of the client request id.
        // A sale comes back with the number the server gave it; a retry with the same number.
        case 'Accepted':
        case 'AlreadyAccepted':
          await _db.markSynced(clientRequestId, recordId,
              documentNumber: result['documentNumber'] as String?);
        case 'Rejected':
          await _db.markFailed(
              clientRequestId, result['error'] as String? ?? 'The office refused this.');
        default:
          await _db.markFailed(clientRequestId, 'The office sent an answer this app did not understand.');
      }
    }
  }

  Future<void> _scheduleRetries(List<OutboxEntry> entries, String reason) async {
    final now = DateTime.now().toUtc();

    for (final entry in entries) {
      final attempt = entry.attemptCount + 1;
      await _db.markForRetry(entry.clientRequestId, attempt, now.add(backoffFor(attempt)), reason);
    }
  }

  // ---------- the server back to the cache ----------

  Future<void> _pull() async {
    final body = await _api.get('/api/mobile/sync/snapshot');

    final customers = (body['customers'] as List<dynamic>? ?? const [])
        .whereType<Map<String, dynamic>>()
        .map((c) => CustomersCompanion.insert(
              id: c['id'] as String,
              name: c['name'] as String,
              contactPerson: Value(c['contactPerson'] as String?),
              phone: Value(c['phone'] as String?),
              address: Value(c['address'] as String?),
              balance: (c['balance'] as num).toDouble(),
              hasMultipleBranches: Value(c['hasMultipleBranches'] as bool? ?? false),
              gstin: Value(c['gstin'] as String?),
            ))
        .toList();

    final products = (body['products'] as List<dynamic>? ?? const [])
        .whereType<Map<String, dynamic>>()
        .map((p) => ProductsCompanion.insert(
              id: p['id'] as String,
              productCode: p['productCode'] as String,
              name: p['name'] as String,
              unitCode: p['unitCode'] as String,
              defaultPrice: Value((p['defaultPrice'] as num?)?.toDouble()),
            ))
        .toList();

    final prices = (body['prices'] as List<dynamic>? ?? const [])
        .whereType<Map<String, dynamic>>()
        .map((p) => CustomerPricesCompanion.insert(
              customerId: p['customerId'] as String,
              productId: p['productId'] as String,
              unitPrice: (p['unitPrice'] as num).toDouble(),
            ))
        .toList();

    final payments = (body['payments'] as List<dynamic>? ?? const [])
        .whereType<Map<String, dynamic>>()
        .map((p) => PaymentsCompanion.insert(
              id: p['id'] as String,
              customerId: p['customerId'] as String,
              paymentDate: p['paymentDate'] as String,
              recordedAt: DateTime.parse(p['recordedAt'] as String),
              amount: (p['amount'] as num).toDouble(),
              method: p['method'] as String,
              reference: Value(p['reference'] as String?),
              notes: Value(p['notes'] as String?),
            ))
        .toList();

    final branches = (body['branches'] as List<dynamic>? ?? const [])
        .whereType<Map<String, dynamic>>()
        .map((b) => BranchesCompanion.insert(
              id: b['id'] as String,
              customerId: b['customerId'] as String,
              name: b['name'] as String,
              location: Value(b['location'] as String?),
              address: Value(b['address'] as String?),
              phone: Value(b['phone'] as String?),
              contactPerson: Value(b['contactPerson'] as String?),
            ))
        .toList();

    await _db.replaceSnapshot(
        customers: customers,
        products: products,
        prices: prices,
        payments: payments,
        branches: branches);

    final pricesAsOf = body['pricesAsOf'];
    if (pricesAsOf is String) await _db.writeMeta(_pricesAsOfKey, pricesAsOf);

    final vanLocation = body['vanLocationId'];
    if (vanLocation is String) await _db.writeMeta(_vanLocationKey, vanLocation);
  }

  /// The office's version of today. Cached on every sync, because the home screen has to show
  /// something sensible in a shop with no signal - with the pending count beside it saying how much
  /// of today the office has not seen yet.
  Future<Map<String, dynamic>?> cachedDay() async {
    final raw = await _db.readMeta(_dayKey);

    return raw == null ? null : Map<String, dynamic>.from(jsonDecode(raw) as Map);
  }

  Future<void> _pullDay() async {
    final body = await _api.get('/api/mobile/day');
    await _db.writeMeta(_dayKey, jsonEncode(body));
  }

  /// What is on the van: loaded today, sold, and what is left. Needs signal, so the screen keeps
  /// the last answer and says when it was from.
  Future<Map<String, dynamic>> vanStock() => _api.get('/api/mobile/van-stock');

  // ---------- status ----------

  Future<String?> get pricesAsOf => _db.readMeta(_pricesAsOfKey);

  Future<DateTime?> get lastSyncedAt async {
    final raw = await _db.readMeta(_lastSyncKey);

    return raw == null ? null : DateTime.tryParse(raw);
  }

  Future<SyncStatus> currentStatus() => _describe(_running ? SyncState.syncing : SyncState.idle);

  Future<SyncStatus> _describe(SyncState state, {String? message}) async {
    final waiting = await _db.entriesWithStatus(OutboxStatus.pending);
    final failed = await _db.entriesWithStatus(OutboxStatus.failed);

    return SyncStatus(
      state: state,
      waiting: waiting.length,
      failed: failed.length,
      lastSyncedAt: await lastSyncedAt,
      message: message,
    );
  }

  void _emit(SyncStatus status) {
    if (!_statusController.isClosed) _statusController.add(status);
  }
}

/// The stored request body, turned back into the fields the API expects.
Map<String, dynamic> decodePayload(String payload) =>
    Map<String, dynamic>.from(jsonDecode(payload) as Map);
