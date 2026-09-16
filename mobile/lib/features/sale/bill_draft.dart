import '../../data/sales_repository.dart';

/// The lines of a bill being written, and nothing else.
///
/// Kept apart from the screen so the one rule that is easy to get wrong - picking a product twice
/// must change the line that is already there, never add a second one - is plain to read and can be
/// tested without pumping a widget.
///
/// It decides no prices. Each line carries the price the synced price list gave for that shop, and
/// this class only ever copies it.
class BillDraft {
  final List<SaleLine> _lines = [];

  List<SaleLine> get lines => List.unmodifiable(_lines);

  bool get isEmpty => _lines.isEmpty;

  /// What the bill comes to on screen. The server recomputes the real one from the same rules the
  /// admin panel uses, so this is for the salesperson's eyes, not for the books.
  double get total => _lines.fold(0, (sum, line) => sum + line.lineTotal);

  bool contains(String productId) => _lines.any((line) => line.productId == productId);

  double quantityOf(String productId) =>
      _lines.where((line) => line.productId == productId).fold(0, (_, line) => line.quantity);

  /// Puts a product on the bill, or adds to it if it is already there.
  ///
  /// Adding rather than replacing is what the salesperson means when they pick the same packet
  /// twice: two of these, then three more. A second line for the same product would be a bill the
  /// server would have to reject anyway.
  void add({
    required String productId,
    required String productName,
    required double unitPrice,
    double quantity = 1,
  }) {
    if (quantity <= 0) return;

    final existing = _indexOf(productId);

    if (existing == -1) {
      _lines.add(SaleLine(
        productId: productId,
        productName: productName,
        quantity: quantity,
        unitPrice: unitPrice,
      ));
      return;
    }

    setQuantity(productId, _lines[existing].quantity + quantity);
  }

  /// Sets a line to an exact quantity. Zero or less removes it, because a line for nothing is not
  /// something anybody meant to write.
  void setQuantity(String productId, double quantity) {
    final index = _indexOf(productId);
    if (index == -1) return;

    if (quantity <= 0) {
      _lines.removeAt(index);
      return;
    }

    final line = _lines[index];
    _lines[index] = SaleLine(
      productId: line.productId,
      productName: line.productName,
      quantity: quantity,
      unitPrice: line.unitPrice,
    );
  }

  void changeQuantity(String productId, double by) =>
      setQuantity(productId, quantityOf(productId) + by);

  void remove(String productId) {
    final index = _indexOf(productId);
    if (index != -1) _lines.removeAt(index);
  }

  int _indexOf(String productId) => _lines.indexWhere((line) => line.productId == productId);
}
