import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../app.dart';
import '../../core/money.dart';
import '../../core/searchable_dropdown.dart';
import '../../data/local/database.dart';
import '../../data/sales_repository.dart';

/// A new bill on one page, laid out like the admin panel's: the shop at the top, a card of product
/// lines, and the total with Save at the bottom. Save then asks credit, paid or part paid.
///
/// The price sits on each line as plain text. There is no field to edit it, because the office
/// decides what each shop pays and the salesperson carries that decision rather than making one.
/// A product the office has never priced for the chosen shop is not offered at all, which is a
/// better failure than an invented number.
///
/// Opened from Home with no shop, or from a shop's page with that shop already chosen.
class SaleScreen extends ConsumerStatefulWidget {
  const SaleScreen({this.shop, super.key});

  final CachedCustomer? shop;

  @override
  ConsumerState<SaleScreen> createState() => _SaleScreenState();
}

/// One row of the products card, as typed. It becomes a [SaleLine] only when saved.
class _Row {
  _Row(this.key);

  final int key;
  String? productId;
  final TextEditingController quantity = TextEditingController();

  double get quantityValue => double.tryParse(quantity.text) ?? 0;
}

class _SaleScreenState extends ConsumerState<SaleScreen> {
  CachedCustomer? _shop;
  List<CachedCustomer> _shops = const [];
  List<CachedProduct> _products = const [];
  Map<String, double?> _prices = const {};
  String? _pricesAsOf;
  bool _loaded = false;
  bool _saving = false;

  final List<_Row> _rows = [];
  int _nextKey = 0;

  @override
  void initState() {
    super.initState();
    _shop = widget.shop;
    _addRow();
    _load();
  }

  @override
  void dispose() {
    for (final row in _rows) {
      row.quantity.dispose();
    }
    super.dispose();
  }

  Future<void> _load() async {
    final db = ref.read(databaseProvider);
    final shops = await db.allCustomers();
    final recent = await db.recentShopNames();
    final products = await db.allProducts();
    final asOf = await ref.read(syncProvider).pricesAsOf;
    final prices = _shop == null ? const <String, double?>{} : await db.pricesFor(_shop!.id);

    // Shops this phone has billed lately first: on a route the next bill is usually nearby.
    final ordered = [
      for (final name in recent) ...shops.where((shop) => shop.name == name),
      ...shops.where((shop) => !recent.contains(shop.name)),
    ];

    if (!mounted) return;

    setState(() {
      _shops = ordered;
      _products = products;
      _prices = prices;
      _pricesAsOf = asOf;
      _loaded = true;
    });
  }

  Future<void> _chooseShop(CachedCustomer? shop) async {
    if (shop == null || shop.id == _shop?.id) return;

    final prices = await ref.read(databaseProvider).pricesFor(shop.id);
    if (!mounted) return;

    setState(() {
      _shop = shop;
      _prices = prices;

      // Prices follow the shop. A product the new shop has no price for cannot stay on the bill.
      for (final row in _rows) {
        if (row.productId != null && prices[row.productId] == null) row.productId = null;
      }
    });
  }

  /// Only what this shop has a price for. Everything else would be a guess.
  List<CachedProduct> get _sellable =>
      _products.where((product) => _prices[product.id] != null).toList();

  void _addRow() => _rows.add(_Row(_nextKey++)..quantity.addListener(_refresh));

  void _refresh() => setState(() {});

  void _removeRow(_Row row) {
    setState(() {
      _rows.remove(row);
      if (_rows.isEmpty) _addRow();
    });
    row.quantity.dispose();
  }

  void _chooseProduct(_Row row, String? productId) {
    setState(() {
      row.productId = productId;
      if (productId != null && row.quantityValue <= 0) row.quantity.text = '1';
    });
  }

  double _priceOf(_Row row) => row.productId == null ? 0 : (_prices[row.productId] ?? 0);

  double _lineTotal(_Row row) => _priceOf(row) * row.quantityValue;

  /// Rows with a product and a quantity. Half-filled rows are simply left off the bill.
  List<SaleLine> get _lines => [
        for (final row in _rows)
          if (row.productId != null && row.quantityValue > 0)
            SaleLine(
              productId: row.productId!,
              productName: _products.firstWhere((p) => p.id == row.productId).name,
              quantity: row.quantityValue,
              unitPrice: _priceOf(row),
            ),
      ];

  double get _total => _lines.fold(0, (sum, line) => sum + line.lineTotal);

  bool get _canSave => _shop != null && _lines.isNotEmpty && !_saving;

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('New bill')),
      body: !_loaded
          ? const Center(child: CircularProgressIndicator())
          : ListView(
              padding: const EdgeInsets.fromLTRB(12, 8, 12, 24),
              children: [
                _shopCard(context),
                const SizedBox(height: 12),
                _productsCard(context),
              ],
            ),
      bottomNavigationBar: _loaded
          ? _Total(total: _total, saving: _saving, onSave: _canSave ? _save : null)
          : null,
    );
  }

  Widget _shopCard(BuildContext context) {
    final shop = _shop;

    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            SearchableDropdown<CachedCustomer>(
              key: const ValueKey('shop'),
              label: 'Shop',
              hintText: 'Search or choose a shop',
              selected: _shops.where((s) => s.id == shop?.id).firstOrNull,
              searchTextOf: (s) => s.phone ?? '',
              onSelected: _chooseShop,
              entries: [
                for (final s in _shops)
                  DropdownMenuEntry(
                    value: s,
                    label: s.name,
                    trailingIcon: s.balance > 0
                        ? Text('owes ${money(s.balance)}',
                            style: TextStyle(
                                fontSize: 12, color: Theme.of(context).colorScheme.error))
                        : null,
                  ),
              ],
            ),
            if (shop != null && shop.balance > 0) ...[
              const SizedBox(height: 8),
              Text(
                'This shop already owes ${money(shop.balance)}.',
                style: TextStyle(fontSize: 12, color: Theme.of(context).colorScheme.error),
              ),
            ],
          ],
        ),
      ),
    );
  }

  Widget _productsCard(BuildContext context) {
    final shop = _shop;

    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Text('Products', style: Theme.of(context).textTheme.titleMedium),
            const SizedBox(height: 12),
            if (shop == null)
              Text('Choose the shop first - what it pays decides what can go on the bill.',
                  style: TextStyle(color: Theme.of(context).hintColor))
            else if (_sellable.isEmpty)
              const _NoPrices()
            else ...[
              for (final row in _rows) ...[
                _lineRow(context, row, shop),
                const Divider(height: 24),
              ],
              Align(
                alignment: Alignment.centerLeft,
                child: OutlinedButton.icon(
                  onPressed: () => setState(_addRow),
                  icon: const Icon(Icons.add),
                  label: const Text('Add product'),
                ),
              ),
            ],
          ],
        ),
      ),
    );
  }

  Widget _lineRow(BuildContext context, _Row row, CachedCustomer shop) {
    // A product already on another line is not offered again, so a bill never names it twice.
    final taken = {
      for (final other in _rows)
        if (other != row && other.productId != null) other.productId,
    };
    final options = _sellable.where((p) => !taken.contains(p.id)).toList();
    final product = _products.where((p) => p.id == row.productId).firstOrNull;
    final hint = Theme.of(context).hintColor;

    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        SearchableDropdown<String>(
          // A new shop re-prices the bill, so the field starts again from what the row now holds.
          key: ValueKey('product-${row.key}-${shop.id}'),
          hintText: 'Search or choose a product',
          selected: row.productId,
          onSelected: (id) => _chooseProduct(row, id),
          entries: [
            for (final p in options)
              DropdownMenuEntry(
                value: p.id,
                label: p.name,
                trailingIcon: Text(money(_prices[p.id]!), style: TextStyle(color: hint)),
              ),
          ],
        ),
        const SizedBox(height: 10),
        Row(
          children: [
            SizedBox(
              width: 88,
              child: TextField(
                key: ValueKey('qty-${row.key}'),
                controller: row.quantity,
                textAlign: TextAlign.right,
                keyboardType: const TextInputType.numberWithOptions(decimal: true),
                inputFormatters: [FilteringTextInputFormatter.allow(RegExp(r'[0-9.]'))],
                decoration: const InputDecoration(labelText: 'Qty', isDense: true),
                onTapOutside: (_) => FocusManager.instance.primaryFocus?.unfocus(),
              ),
            ),
            const SizedBox(width: 12),
            Expanded(
              child: Text(
                // Read-only, deliberately. The office sets what this shop pays.
                product == null
                    ? ''
                    : '× ${money(_priceOf(row))} per ${product.unitCode.toLowerCase()}',
                style: TextStyle(color: hint),
              ),
            ),
            Text(money(_lineTotal(row)), style: const TextStyle(fontWeight: FontWeight.w600)),
            IconButton(
              tooltip: 'Remove line',
              icon: const Icon(Icons.delete_outline),
              onPressed: () => _removeRow(row),
            ),
          ],
        ),
      ],
    );
  }

  Future<void> _save() async {
    final shop = _shop!;
    final lines = _lines;
    final total = _total;

    final choice = await showModalBottomSheet<_HowPaid>(
      context: context,
      isScrollControlled: true,
      builder: (context) => _PaymentChoice(total: total),
    );

    if (choice == null || !mounted) return;

    setState(() => _saving = true);

    try {
      await ref.read(salesRepositoryProvider).recordSale(
            customerId: shop.id,
            customerName: shop.name,
            lines: lines,
            amountPaid: choice.amount,
            paymentMethod: choice.method,
            pricesAsOf: _pricesAsOf,
          );

      // Saved locally already; this only tries to hand it over and never blocks the salesperson.
      unawaitedSync(ref);

      if (!mounted) return;

      Navigator.of(context).pop();
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text('${money(total)} recorded for ${shop.name}.')),
      );
    } finally {
      if (mounted) setState(() => _saving = false);
    }
  }
}

class _Total extends StatelessWidget {
  const _Total({required this.total, required this.saving, required this.onSave});

  final double total;

  /// Two taps would make two separate sales - each with its own client request id, so the server
  /// could not tell them apart. The button closes the moment the first one starts.
  final bool saving;

  final VoidCallback? onSave;

  @override
  Widget build(BuildContext context) => SafeArea(
        child: Padding(
          padding: const EdgeInsets.all(12),
          child: Row(
            children: [
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    Text('Total', style: TextStyle(color: Theme.of(context).hintColor)),
                    Text(money(total),
                        style: Theme.of(context)
                            .textTheme
                            .headlineSmall
                            ?.copyWith(fontWeight: FontWeight.w600)),
                  ],
                ),
              ),
              SizedBox(
                width: 150,
                child: FilledButton(
                  onPressed: saving ? null : onSave,
                  child: saving
                      ? const SizedBox(
                          height: 20, width: 20, child: CircularProgressIndicator(strokeWidth: 2))
                      : const Text('Save'),
                ),
              ),
            ],
          ),
        ),
      );
}

class _HowPaid {
  const _HowPaid(this.amount, this.method);

  final double amount;
  final String method;
}

/// Credit or paid, in one tap each. Part payment is there for the case that actually happens -
/// the shop hands over what it has - without making the common cases slower.
class _PaymentChoice extends StatefulWidget {
  const _PaymentChoice({required this.total});

  final double total;

  @override
  State<_PaymentChoice> createState() => _PaymentChoiceState();
}

class _PaymentChoiceState extends State<_PaymentChoice> {
  final _amount = TextEditingController();
  bool _partial = false;
  String _method = 'Cash';

  @override
  void dispose() {
    _amount.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) => SafeArea(
        child: Padding(
          padding: EdgeInsets.fromLTRB(
              16, 16, 16, 16 + MediaQuery.of(context).viewInsets.bottom),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              Text(money(widget.total),
                  textAlign: TextAlign.center,
                  style: Theme.of(context)
                      .textTheme
                      .headlineMedium
                      ?.copyWith(fontWeight: FontWeight.w600)),
              const SizedBox(height: 20),
              if (!_partial) ...[
                FilledButton(
                  onPressed: () => Navigator.of(context).pop(_HowPaid(widget.total, _method)),
                  child: Text('Paid ${money(widget.total)} now'),
                ),
                const SizedBox(height: 10),
                OutlinedButton(
                  style: OutlinedButton.styleFrom(minimumSize: const Size.fromHeight(52)),
                  onPressed: () => Navigator.of(context).pop(const _HowPaid(0, 'Cash')),
                  child: const Text('On credit'),
                ),
                const SizedBox(height: 6),
                TextButton(
                  onPressed: () => setState(() => _partial = true),
                  child: const Text('Paid part of it'),
                ),
              ] else ...[
                TextField(
                  controller: _amount,
                  autofocus: true,
                  keyboardType: const TextInputType.numberWithOptions(decimal: true),
                  decoration: const InputDecoration(labelText: 'Amount received'),
                ),
                const SizedBox(height: 12),
                SegmentedButton<String>(
                  segments: const [
                    ButtonSegment(value: 'Cash', label: Text('Cash')),
                    ButtonSegment(value: 'UPI', label: Text('UPI')),
                  ],
                  selected: {_method},
                  onSelectionChanged: (values) => setState(() => _method = values.first),
                ),
                const SizedBox(height: 16),
                FilledButton(
                  onPressed: () {
                    final amount = double.tryParse(_amount.text) ?? 0;
                    Navigator.of(context).pop(_HowPaid(amount, _method));
                  },
                  child: const Text('Save'),
                ),
              ],
            ],
          ),
        ),
      );
}

class _NoPrices extends StatelessWidget {
  const _NoPrices();

  @override
  Widget build(BuildContext context) => Center(
        child: Padding(
          padding: const EdgeInsets.all(32),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              const Icon(Icons.sell_outlined, size: 40),
              const SizedBox(height: 12),
              Text('No prices for this shop yet',
                  style: Theme.of(context).textTheme.titleMedium, textAlign: TextAlign.center),
              const SizedBox(height: 4),
              const Text(
                'The office sets what each shop pays. Ask them to add it, then sync.',
                textAlign: TextAlign.center,
              ),
            ],
          ),
        ),
      );
}
