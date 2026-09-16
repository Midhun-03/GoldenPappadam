import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../app.dart';
import '../../core/money.dart';
import '../../data/local/database.dart';
import '../../data/sales_repository.dart';
import 'bill_draft.dart';

/// Shop, products, quantities, then credit or paid. Nothing else.
///
/// The price sits beside each product as plain text. There is no field to edit it, because the
/// office decides what each shop pays and the salesperson carries that decision rather than making
/// one. A product the office has never priced cannot be added at all, which is a better failure
/// than an invented number.
///
/// Products are picked from a search rather than listed permanently: a route sells four or five
/// things out of a catalogue that will only grow, and scrolling past the rest every time is a tax
/// on every bill.
class SaleScreen extends ConsumerStatefulWidget {
  const SaleScreen({required this.shop, super.key});

  final CachedCustomer shop;

  @override
  ConsumerState<SaleScreen> createState() => _SaleScreenState();
}

class _SaleScreenState extends ConsumerState<SaleScreen> {
  final BillDraft _draft = BillDraft();
  bool _saving = false;

  List<CachedProduct> _products = const [];
  Map<String, double?> _prices = const {};
  String? _pricesAsOf;
  bool _loaded = false;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    final db = ref.read(databaseProvider);
    final products = await db.allProducts();
    final prices = await db.pricesFor(widget.shop.id);
    final asOf = await ref.read(syncProvider).pricesAsOf;

    if (!mounted) return;

    setState(() {
      _products = products;
      _prices = prices;
      _pricesAsOf = asOf;
      _loaded = true;
    });
  }

  /// Only what this shop has a price for. Everything else would be a guess.
  List<CachedProduct> get _sellable =>
      _products.where((product) => _prices[product.id] != null).toList();

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: Text(widget.shop.name),
        bottom: widget.shop.balance > 0
            ? PreferredSize(
                preferredSize: const Size.fromHeight(28),
                child: Padding(
                  padding: const EdgeInsets.only(bottom: 8),
                  child: Text(
                    'Owes ${money(widget.shop.balance)}',
                    style: TextStyle(color: Theme.of(context).colorScheme.error),
                  ),
                ),
              )
            : null,
      ),
      body: !_loaded
          ? const Center(child: CircularProgressIndicator())
          : _sellable.isEmpty
              ? const _NoPrices()
              : _draft.isEmpty
                  ? _EmptyBill(onAdd: _pickProduct)
                  : ListView(
                      padding: const EdgeInsets.only(bottom: 16),
                      children: [
                        for (final line in _draft.lines)
                          _LineRow(
                            line: line,
                            unit: _unitOf(line.productId),
                            onChanged: (quantity) =>
                                setState(() => _draft.setQuantity(line.productId, quantity)),
                            onStep: (by) =>
                                setState(() => _draft.changeQuantity(line.productId, by)),
                            onRemove: () => setState(() => _draft.remove(line.productId)),
                          ),
                        Padding(
                          padding: const EdgeInsets.fromLTRB(12, 12, 12, 0),
                          child: OutlinedButton.icon(
                            onPressed: _pickProduct,
                            icon: const Icon(Icons.add),
                            label: const Text('Add product'),
                            style: OutlinedButton.styleFrom(minimumSize: const Size.fromHeight(48)),
                          ),
                        ),
                      ],
                    ),
      bottomNavigationBar:
          _draft.isEmpty ? null : _Total(total: _draft.total, saving: _saving, onSave: _save),
    );
  }

  String _unitOf(String productId) =>
      _products.firstWhere((product) => product.id == productId).unitCode;

  /// Picking a product already on the bill adds to that line rather than making a second one.
  Future<void> _pickProduct() async {
    final product = await showModalBottomSheet<CachedProduct>(
      context: context,
      isScrollControlled: true,
      builder: (context) => _ProductPicker(products: _sellable, prices: _prices, draft: _draft),
    );

    if (product == null || !mounted) return;

    setState(() => _draft.add(
          productId: product.id,
          productName: product.name,
          unitPrice: _prices[product.id]!,
        ));
  }

  Future<void> _save() async {
    final choice = await showModalBottomSheet<_HowPaid>(
      context: context,
      builder: (context) => _PaymentChoice(total: _draft.total),
    );

    if (choice == null || !mounted) return;

    setState(() => _saving = true);
    final total = _draft.total;

    try {
      await ref.read(salesRepositoryProvider).recordSale(
            customerId: widget.shop.id,
            customerName: widget.shop.name,
            lines: _draft.lines,
            amountPaid: choice.amount,
            paymentMethod: choice.method,
            pricesAsOf: _pricesAsOf,
          );

      // Saved locally already; this only tries to hand it over and never blocks the salesperson.
      unawaitedSync(ref);

      if (!mounted) return;

      Navigator.of(context).pop();
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text('${money(total)} recorded for ${widget.shop.name}.')),
      );
    } finally {
      if (mounted) setState(() => _saving = false);
    }
  }
}

/// One product on the bill: what it is, what this shop pays, and how many.
class _LineRow extends StatelessWidget {
  const _LineRow({
    required this.line,
    required this.unit,
    required this.onChanged,
    required this.onStep,
    required this.onRemove,
  });

  final SaleLine line;
  final String unit;
  final void Function(double) onChanged;
  final void Function(double) onStep;
  final VoidCallback onRemove;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 8),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(line.productName, style: const TextStyle(fontWeight: FontWeight.w500)),
                    Text(
                      // Read-only, deliberately. The office sets what this shop pays.
                      '${money(line.unitPrice)} per ${unit.toLowerCase()}',
                      style: TextStyle(fontSize: 12, color: Theme.of(context).hintColor),
                    ),
                  ],
                ),
              ),
              Text(
                money(line.lineTotal),
                style: const TextStyle(fontWeight: FontWeight.w600),
              ),
              IconButton(
                tooltip: 'Remove',
                icon: const Icon(Icons.close, size: 18),
                onPressed: onRemove,
              ),
            ],
          ),
          Row(
            children: [
              // Big targets: this is used one-handed, standing up, often in a hurry.
              IconButton.filledTonal(
                onPressed: () => onStep(-1),
                icon: const Icon(Icons.remove),
              ),
              SizedBox(
                width: 72,
                child: TextField(
                  key: ValueKey('qty-${line.productId}-${line.quantity}'),
                  controller: TextEditingController(text: _label(line.quantity)),
                  textAlign: TextAlign.center,
                  keyboardType: const TextInputType.numberWithOptions(decimal: true),
                  inputFormatters: [FilteringTextInputFormatter.allow(RegExp(r'[0-9.]'))],
                  decoration: const InputDecoration(isDense: true),
                  onSubmitted: (value) => onChanged(double.tryParse(value) ?? 0),
                  onTapOutside: (_) => FocusManager.instance.primaryFocus?.unfocus(),
                ),
              ),
              IconButton.filledTonal(
                onPressed: () => onStep(1),
                icon: const Icon(Icons.add),
              ),
            ],
          ),
        ],
      ),
    );
  }

  static String _label(double value) =>
      value == value.roundToDouble() ? value.toStringAsFixed(0) : value.toString();
}

/// Search rather than scroll. The catalogue only grows; a route sells a handful of things.
class _ProductPicker extends StatefulWidget {
  const _ProductPicker({required this.products, required this.prices, required this.draft});

  final List<CachedProduct> products;
  final Map<String, double?> prices;
  final BillDraft draft;

  @override
  State<_ProductPicker> createState() => _ProductPickerState();
}

class _ProductPickerState extends State<_ProductPicker> {
  String _term = '';

  @override
  Widget build(BuildContext context) {
    final needle = _term.trim().toLowerCase();
    final matches = needle.isEmpty
        ? widget.products
        : widget.products.where((p) => p.name.toLowerCase().contains(needle)).toList();

    return Padding(
      padding: EdgeInsets.only(bottom: MediaQuery.of(context).viewInsets.bottom),
      child: SizedBox(
        height: MediaQuery.of(context).size.height * 0.7,
        child: Column(
          children: [
            Padding(
              padding: const EdgeInsets.all(12),
              child: TextField(
                autofocus: true,
                onChanged: (value) => setState(() => _term = value),
                decoration: const InputDecoration(
                  hintText: 'Search or select product',
                  prefixIcon: Icon(Icons.search),
                ),
              ),
            ),
            Expanded(
              child: matches.isEmpty
                  ? const Center(child: Text('Nothing by that name.'))
                  : ListView.separated(
                      itemCount: matches.length,
                      separatorBuilder: (_, _) => const Divider(height: 1),
                      itemBuilder: (context, index) {
                        final product = matches[index];
                        final already = widget.draft.quantityOf(product.id);

                        return ListTile(
                          title: Text(product.name),
                          subtitle: Text(
                            '${money(widget.prices[product.id]!)} per ${product.unitCode.toLowerCase()}',
                          ),
                          // Says plainly that picking it again adds to the line already there.
                          trailing: already > 0
                              ? Chip(label: Text('${_LineRow._label(already)} on bill'))
                              : const Icon(Icons.add),
                          onTap: () => Navigator.of(context).pop(product),
                        );
                      },
                    ),
            ),
          ],
        ),
      ),
    );
  }
}

class _EmptyBill extends StatelessWidget {
  const _EmptyBill({required this.onAdd});

  final VoidCallback onAdd;

  @override
  Widget build(BuildContext context) => Center(
        child: Padding(
          padding: const EdgeInsets.all(32),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              const Icon(Icons.receipt_long_outlined, size: 40),
              const SizedBox(height: 12),
              Text('Nothing on this bill yet', style: Theme.of(context).textTheme.titleMedium),
              const SizedBox(height: 4),
              const Text('Add what you delivered.', textAlign: TextAlign.center),
              const SizedBox(height: 20),
              FilledButton.icon(
                onPressed: onAdd,
                icon: const Icon(Icons.add),
                label: const Text('Add product'),
              ),
            ],
          ),
        ),
      );
}

class _Total extends StatelessWidget {
  const _Total({required this.total, required this.saving, required this.onSave});

  final double total;

  /// Two taps would make two separate sales - each with its own client request id, so the server
  /// could not tell them apart. The button closes the moment the first one starts.
  final bool saving;

  final VoidCallback onSave;

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
