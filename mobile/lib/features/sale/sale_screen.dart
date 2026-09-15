import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../app.dart';
import '../../core/money.dart';
import '../../data/local/database.dart';
import '../../data/sales_repository.dart';

/// Shop, products, quantities, then credit or paid. Nothing else.
///
/// The price sits beside each product as plain text. There is no field to edit it, because the
/// office decides what each shop pays and the salesperson carries that decision rather than making
/// one. A product the office has never priced simply cannot be added, which is a better failure
/// than an invented number.
class SaleScreen extends ConsumerStatefulWidget {
  const SaleScreen({required this.shop, super.key});

  final CachedCustomer shop;

  @override
  ConsumerState<SaleScreen> createState() => _SaleScreenState();
}

class _SaleScreenState extends ConsumerState<SaleScreen> {
  final Map<String, double> _quantities = {};
  final _search = TextEditingController();
  String _term = '';
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

  @override
  void dispose() {
    _search.dispose();
    super.dispose();
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

  List<SaleLine> get _lines => [
        for (final entry in _quantities.entries)
          if (entry.value > 0 && _prices[entry.key] != null)
            SaleLine(
              productId: entry.key,
              productName: _products.firstWhere((p) => p.id == entry.key).name,
              quantity: entry.value,
              unitPrice: _prices[entry.key]!,
            )
      ];

  double get _total => _lines.fold(0, (sum, line) => sum + line.lineTotal);

  @override
  Widget build(BuildContext context) {
    // Only what this shop has a price for. Everything else would be a guess.
    final sellable = _products.where((product) => _prices[product.id] != null).toList();
    final visible = _term.trim().isEmpty
        ? sellable
        : sellable
            .where((p) => p.name.toLowerCase().contains(_term.trim().toLowerCase()))
            .toList();

    return Scaffold(
      appBar: AppBar(title: Text(widget.shop.name)),
      body: !_loaded
          ? const Center(child: CircularProgressIndicator())
          : sellable.isEmpty
              ? const _NoPrices()
              : Column(
                  children: [
                    Padding(
                      padding: const EdgeInsets.fromLTRB(12, 12, 12, 4),
                      child: TextField(
                        controller: _search,
                        onChanged: (value) => setState(() => _term = value),
                        decoration: const InputDecoration(
                          hintText: 'Find a product',
                          prefixIcon: Icon(Icons.search),
                          isDense: true,
                        ),
                      ),
                    ),
                    Expanded(
                      child: ListView.separated(
                        itemCount: visible.length,
                        separatorBuilder: (_, _) => const Divider(height: 1),
                        itemBuilder: (context, index) {
                          final product = visible[index];

                          return _ProductRow(
                            product: product,
                            price: _prices[product.id]!,
                            quantity: _quantities[product.id] ?? 0,
                            onChanged: (value) => setState(() {
                              if (value <= 0) {
                                _quantities.remove(product.id);
                              } else {
                                _quantities[product.id] = value;
                              }
                            }),
                          );
                        },
                      ),
                    ),
                  ],
                ),
      bottomNavigationBar:
          _lines.isEmpty ? null : _Total(total: _total, saving: _saving, onSave: _save),
    );
  }

  Future<void> _save() async {
    final choice = await showModalBottomSheet<_HowPaid>(
      context: context,
      builder: (context) => _PaymentChoice(total: _total),
    );

    if (choice == null || !mounted) return;

    setState(() => _saving = true);

    try {
      await ref.read(salesRepositoryProvider).recordSale(
            customerId: widget.shop.id,
            customerName: widget.shop.name,
            lines: _lines,
            amountPaid: choice.amount,
            paymentMethod: choice.method,
            pricesAsOf: _pricesAsOf,
          );

      // Saved locally already; this only tries to hand it over and never blocks the salesperson.
      unawaitedSync(ref);

      if (!mounted) return;

      Navigator.of(context).pop();
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text('${money(_total)} recorded for ${widget.shop.name}.')),
      );
    } finally {
      if (mounted) setState(() => _saving = false);
    }
  }
}

class _ProductRow extends StatelessWidget {
  const _ProductRow({
    required this.product,
    required this.price,
    required this.quantity,
    required this.onChanged,
  });

  final CachedProduct product;
  final double price;
  final double quantity;
  final void Function(double) onChanged;

  @override
  Widget build(BuildContext context) {
    final chosen = quantity > 0;

    return Container(
      color: chosen ? Theme.of(context).colorScheme.primaryContainer.withValues(alpha: 0.3) : null,
      padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 6),
      child: Row(
        children: [
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(product.name, style: const TextStyle(fontWeight: FontWeight.w500)),
                Text(
                  '${money(price)} per ${product.unitCode.toLowerCase()}',
                  style: TextStyle(fontSize: 12, color: Theme.of(context).hintColor),
                ),
              ],
            ),
          ),
          // Big targets: this is used one-handed, standing up, often in a hurry.
          IconButton.filledTonal(
            onPressed: quantity > 0 ? () => onChanged(quantity - 1) : null,
            icon: const Icon(Icons.remove),
          ),
          SizedBox(
            width: 56,
            child: TextField(
              key: ValueKey('qty-${product.id}-$quantity'),
              controller: TextEditingController(
                text: quantity == 0 ? '' : quantity.toStringAsFixed(0),
              ),
              textAlign: TextAlign.center,
              keyboardType: const TextInputType.numberWithOptions(decimal: true),
              inputFormatters: [FilteringTextInputFormatter.allow(RegExp(r'[0-9.]'))],
              decoration: const InputDecoration(isDense: true, hintText: '0'),
              onSubmitted: (value) => onChanged(double.tryParse(value) ?? 0),
              onTapOutside: (_) => FocusManager.instance.primaryFocus?.unfocus(),
            ),
          ),
          IconButton.filledTonal(
            onPressed: () => onChanged(quantity + 1),
            icon: const Icon(Icons.add),
          ),
        ],
      ),
    );
  }
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
