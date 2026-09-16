import 'package:drift/drift.dart';
import 'package:uuid/uuid.dart';

import 'local/database.dart';

/// One line of a sale as the salesperson entered it. The price came from the synced price list and
/// is carried, not chosen: there is no screen in this app that offers to change it.
class SaleLine {
  const SaleLine({
    required this.productId,
    required this.productName,
    required this.quantity,
    required this.unitPrice,
  });

  final String productId;
  final String productName;
  final double quantity;
  final double unitPrice;

  /// For the running total on screen. The server recomputes the real one.
  double get lineTotal => quantity * unitPrice;
}

const _uuid = Uuid();

/// Writes down what happened at a shop.
///
/// Everything here lands in the outbox and nowhere else. The method returns as soon as the local
/// database has it, which is what lets the salesperson keep walking whether or not there is signal.
class SalesRepository {
  SalesRepository(this._db);

  final AppDatabase _db;

  /// A delivery, optionally paid for on the spot, always with the visit that produced it.
  ///
  /// The three are written in one transaction so a sale can never exist without the stop that
  /// explains it, and the visit names the other two by their client ids - because at this moment
  /// the phone has never spoken to the server and knows no other name for them.
  Future<String> recordSale({
    required String customerId,
    required String customerName,
    required List<SaleLine> lines,
    required double amountPaid,
    required String paymentMethod,
    String? notes,
    String? pricesAsOf,
  }) async {
    if (lines.isEmpty) {
      throw ArgumentError('A sale needs at least one product.');
    }

    final recordedAt = DateTime.now().toUtc();
    final saleId = _uuid.v4();
    final total = lines.fold<double>(0, (sum, line) => sum + line.lineTotal);
    final paymentId = amountPaid > 0 ? _uuid.v4() : null;

    await _db.transaction(() async {
      await _db.enqueue(
        clientRequestId: saleId,
        type: 'Invoice',
        recordedAt: recordedAt,
        summary: '$customerName · ${_money(total)}',
        payload: {
          'sale': {
            'customerId': customerId,
            'lines': [
              for (final line in lines)
                {
                  'productId': line.productId,
                  'quantity': line.quantity,
                  'unitPrice': line.unitPrice,
                }
            ],
            'pricesAsOf': pricesAsOf ?? recordedAt.toIso8601String(),
            'notes': notes,
          }
        },
      );

      if (paymentId != null) {
        await _db.enqueue(
          clientRequestId: paymentId,
          type: 'Payment',
          recordedAt: recordedAt,
          summary: '$customerName · ${_money(amountPaid)} received',
          payload: {
            'payment': {
              'customerId': customerId,
              'amount': amountPaid,
              'method': paymentMethod,
              'reference': null,
              'notes': null,
            }
          },
        );
      }

      await _enqueueVisit(
        customerId: customerId,
        customerName: customerName,
        outcome: 'Sold',
        recordedAt: recordedAt,
        saleClientRequestId: saleId,
        paymentClientRequestId: paymentId,
      );
    });

    return saleId;
  }

  /// Money against what the shop already owed, with no delivery today. The common case where a
  /// shop is settling last week's bills.
  Future<String> recordPayment({
    required String customerId,
    required String customerName,
    required double amount,
    required String method,
    String? reference,
    String? notes,
  }) async {
    final recordedAt = DateTime.now().toUtc();
    final paymentId = _uuid.v4();

    await _db.transaction(() async {
      await _db.enqueue(
        clientRequestId: paymentId,
        type: 'Payment',
        recordedAt: recordedAt,
        summary: '$customerName · ${_money(amount)} received',
        payload: {
          'payment': {
            'customerId': customerId,
            'amount': amount,
            'method': method,
            'reference': reference,
            'notes': notes,
          }
        },
      );

      await _enqueueVisit(
        customerId: customerId,
        customerName: customerName,
        outcome: 'Sold',
        recordedAt: recordedAt,
        paymentClientRequestId: paymentId,
      );
    });

    return paymentId;
  }

  /// Stopped, sold nothing. Worth a record: it is the only way the office ever learns that the
  /// shop was visited and did not need anything.
  Future<String> recordVisit({
    required String customerId,
    required String customerName,
    required String outcome,
    String? notes,
  }) async {
    final recordedAt = DateTime.now().toUtc();

    return _enqueueVisit(
      customerId: customerId,
      customerName: customerName,
      outcome: outcome,
      recordedAt: recordedAt,
      notes: notes,
    );
  }

  /// What the salesperson actually took from the warehouse this morning.
  ///
  /// The payload names no van and no direction: the server puts it on this phone's own van, from
  /// the main warehouse. That is deliberately the only stock a salesperson can move.
  Future<String> recordVanLoad({
    required List<SaleLine> lines,
    String? notes,
  }) async {
    if (lines.isEmpty) {
      throw ArgumentError('A load needs at least one product.');
    }

    final id = _uuid.v4();
    final total = lines.fold<double>(0, (sum, line) => sum + line.quantity);

    await _db.enqueue(
      clientRequestId: id,
      type: 'VanLoad',
      recordedAt: DateTime.now().toUtc(),
      summary: 'Van load · ${quantityLabel(total)} items',
      payload: {
        'vanLoad': {
          'lines': [
            for (final line in lines)
              {'productId': line.productId, 'quantity': line.quantity}
          ],
          'notes': notes,
        }
      },
    );

    return id;
  }

  /// What the salesperson needs the packing unit to pack, and when. Not a customer order: nothing
  /// is billed and no stock moves.
  Future<String> recordStockRequest({
    required DateTime requiredDate,
    required List<SaleLine> lines,
    String? notes,
  }) async {
    if (lines.isEmpty) {
      throw ArgumentError('A request needs at least one product.');
    }

    final id = _uuid.v4();
    final day = requiredDate.toIso8601String().substring(0, 10);

    await _db.enqueue(
      clientRequestId: id,
      type: 'StockRequest',
      recordedAt: DateTime.now().toUtc(),
      summary: 'Asked for ${lines.length} product${lines.length == 1 ? '' : 's'} for $day',
      payload: {
        'stockRequest': {
          'requiredDate': day,
          'lines': [
            for (final line in lines)
              {'productId': line.productId, 'quantity': line.quantity}
          ],
          'notes': notes,
        }
      },
    );

    return id;
  }

  /// Payments this phone has taken that the office has not confirmed, newest first.
  Future<List<OutboxEntry>> unsentPaymentsFor(String customerName) async {
    final entries = await _db.watchUnfinished().first;

    return entries
        .where((entry) => entry.type == 'Payment' && entry.summary.startsWith('$customerName ·'))
        .toList();
  }

  /// What this shop has waiting to go up, so the screen can show the salesperson their own morning
  /// even with no signal.
  Future<List<OutboxEntry>> unsentFor(String customerName) async {
    final entries = await _db.watchUnfinished().first;

    return entries.where((entry) => entry.summary.startsWith('$customerName ·')).toList();
  }

  Future<String> _enqueueVisit({
    required String customerId,
    required String customerName,
    required String outcome,
    required DateTime recordedAt,
    String? saleClientRequestId,
    String? paymentClientRequestId,
    String? notes,
  }) async {
    final visitId = _uuid.v4();

    await _db.enqueue(
      clientRequestId: visitId,
      type: 'Visit',
      recordedAt: recordedAt,
      summary: '$customerName · visit',
      payload: {
        'visit': {
          'customerId': customerId,
          'outcome': outcome,
          'saleClientRequestId': saleClientRequestId,
          'paymentClientRequestId': paymentClientRequestId,
          'notes': notes,
        }
      },
    );

    return visitId;
  }

  static String _money(double value) => '₹${value.toStringAsFixed(2)}';
}

/// Whole numbers read better than "350.0 items" on a load sheet.
String quantityLabel(double value) =>
    value == value.roundToDouble() ? value.toStringAsFixed(0) : value.toStringAsFixed(3);

/// The last balance the office told us, and when. Shown with the "as of" so nobody mistakes a
/// stale figure for a live one.
class ShopBalance {
  const ShopBalance({required this.balance, required this.asOf});

  final double balance;
  final DateTime? asOf;
}

extension ShopQueries on AppDatabase {
  Future<List<CachedCustomer>> searchShops(String term) async {
    final all = await allCustomers();
    final needle = term.trim().toLowerCase();

    if (needle.isEmpty) return all;

    return all
        .where((shop) =>
            shop.name.toLowerCase().contains(needle) ||
            (shop.phone ?? '').toLowerCase().contains(needle))
        .toList();
  }
}

/// Exposed so the sale screen and the tests agree on what a companion looks like.
CustomersCompanion shopRow({
  required String id,
  required String name,
  required double balance,
  String? phone,
}) =>
    CustomersCompanion.insert(
      id: id,
      name: name,
      balance: balance,
      phone: Value(phone),
    );
