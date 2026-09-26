import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../app.dart';
import '../../core/searchable_dropdown.dart';
import '../../core/theme.dart';
import '../../core/widgets.dart';
import '../../data/local/database.dart';
import '../../data/sales_repository.dart';

/// Expired or damaged packets a shop handed back, opened from that shop's page.
///
/// Three short steps: which branch (only for a multi-branch shop), how many of each product came
/// back and why, and whether fresh packets went to the shop from the van. There is no money on
/// this screen at all: the office decides what the packets were worth and whether the shop is
/// credited, so the salesperson never promises a figure on the doorstep.
class ReturnScreen extends ConsumerStatefulWidget {
  const ReturnScreen({required this.shop, super.key});

  final CachedCustomer shop;

  @override
  ConsumerState<ReturnScreen> createState() => _ReturnScreenState();
}

class _ReturnScreenState extends ConsumerState<ReturnScreen> {
  List<CachedProduct> _products = const [];
  List<CachedBranch> _branches = const [];
  CachedBranch? _branch;
  bool _hasVan = false;
  bool _loaded = false;
  bool _saving = false;

  final Map<String, TextEditingController> _qtyControllers = {};
  final Map<String, String> _reasons = {};

  /// Null until the salesperson answers: handing over fresh packets moves van stock, so it is
  /// never assumed either way.
  bool? _replaced;

  @override
  void initState() {
    super.initState();
    _load();
  }

  @override
  void dispose() {
    for (final controller in _qtyControllers.values) {
      controller.dispose();
    }
    super.dispose();
  }

  Future<void> _load() async {
    final db = ref.read(databaseProvider);
    final products = await db.allProducts();
    final prices = await db.pricesFor(widget.shop.id);
    final branches =
        widget.shop.hasMultipleBranches ? await db.branchesFor(widget.shop.id) : const <CachedBranch>[];
    final van = await ref.read(syncProvider).vanLocationId;

    // What this shop buys first - that is what it can give back - then everything else, since an
    // old product may no longer be priced for it.
    final priced = products.where((p) => prices[p.id] != null).toList();
    final rest = products.where((p) => prices[p.id] == null).toList();

    if (!mounted) return;

    setState(() {
      _products = [...priced, ...rest];
      _branches = branches;
      _hasVan = van != null;
      // With no van there is nothing to hand over, so the only answer is the office's.
      _replaced = van == null ? false : null;
      _loaded = true;
    });
  }

  TextEditingController _controllerFor(String productId) =>
      _qtyControllers.putIfAbsent(productId, TextEditingController.new);

  double _quantityOf(CachedProduct product) =>
      double.tryParse(_qtyControllers[product.id]?.text ?? '') ?? 0;

  List<ReturnLine> get _lines => [
        for (final product in _products)
          if (_quantityOf(product) > 0)
            ReturnLine(
              productId: product.id,
              productName: product.name,
              quantity: _quantityOf(product),
              reason: _reasons[product.id] ?? 'Expired',
            ),
      ];

  double get _total => _lines.fold(0, (sum, line) => sum + line.quantity);

  bool get _branchIsValid => !widget.shop.hasMultipleBranches || _branch != null;

  bool get _canSave => _lines.isNotEmpty && _branchIsValid && _replaced != null && !_saving;

  @override
  Widget build(BuildContext context) {
    var step = 0;

    return Scaffold(
      appBar: AppBar(
        toolbarHeight: 64,
        title: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          mainAxisSize: MainAxisSize.min,
          children: [
            const Text('EXPIRED OR DAMAGED', style: kEyebrowStyle),
            Text(widget.shop.name, style: Theme.of(context).textTheme.titleLarge),
          ],
        ),
      ),
      body: !_loaded
          ? const Center(child: CircularProgressIndicator())
          : FadeIn(
              child: ListView(
                padding: const EdgeInsets.fromLTRB(12, 4, 12, 24),
                children: [
                  if (widget.shop.hasMultipleBranches) ...[
                    NumberedStepHeader(++step, 'Branch'),
                    _branchPicker(),
                  ],
                  NumberedStepHeader(++step, 'Packets collected', caption: 'Never resold'),
                  _productsCard(),
                  NumberedStepHeader(++step, 'Fresh packets given?'),
                  _replacementCard(),
                ],
              ),
            ),
      bottomNavigationBar: _loaded ? _saveBar(context) : null,
    );
  }

  Widget _branchPicker() => SearchableDropdown<CachedBranch>(
        key: const ValueKey('return-branch'),
        hintText: 'Search or select branch',
        helperText: _branch == null ? 'Required before saving' : null,
        selected: _branches.where((b) => b.id == _branch?.id).firstOrNull,
        searchTextOf: (b) => b.location ?? '',
        onSelected: (branch) => setState(() => _branch = branch),
        entries: [for (final b in _branches) DropdownMenuEntry(value: b, label: b.name)],
      );

  Widget _productsCard() {
    if (_products.isEmpty) {
      return const AppCard(
        child: EmptyState(
          icon: Icons.inventory_2_outlined,
          title: 'No products yet',
          message: 'Sync once with a signal to get the product list.',
        ),
      );
    }

    return AppCard(
      padding: const EdgeInsets.symmetric(horizontal: 14),
      child: Column(
        children: [
          for (final product in _products) ...[
            _productRow(product),
            if (product != _products.last) const Divider(height: 22),
          ],
        ],
      ),
    );
  }

  Widget _productRow(CachedProduct product) {
    final counted = _quantityOf(product) > 0;
    final reason = _reasons[product.id] ?? 'Expired';

    return Padding(
      key: ValueKey('return-row-${product.id}'),
      padding: const EdgeInsets.symmetric(vertical: 6),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Row(
            children: [
              Expanded(
                child: Text(product.name, style: const TextStyle(fontWeight: FontWeight.w700)),
              ),
              const SizedBox(width: 10),
              QuantityStepper(
                controller: _controllerFor(product.id),
                fieldKey: ValueKey('return-qty-${product.id}'),
                onChanged: (_) => setState(() {}),
              ),
            ],
          ),
          // Why they came back, asked only for what actually came back.
          if (counted) ...[
            const SizedBox(height: 8),
            ChoiceRow<String>(
              key: ValueKey('return-reason-${product.id}'),
              value: reason,
              onChanged: (value) => setState(() => _reasons[product.id] = value),
              options: const [('Expired', 'Expired'), ('Damaged', 'Damaged')],
            ),
          ],
        ],
      ),
    );
  }

  Widget _replacementCard() {
    if (!_hasVan) {
      return const AppCard(
        child: Text(
          'This phone has no van, so nothing can be handed over from one. '
          'The office will decide what the shop gets.',
          style: TextStyle(color: AppColors.textMuted),
        ),
      );
    }

    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        // Buttons rather than a switch: the answer changes van stock, so it has to be chosen.
        Row(
          children: [
            Expanded(
              child: _Answer(
                key: const ValueKey('replaced-yes'),
                label: 'Yes, from the van',
                selected: _replaced == true,
                onTap: () => setState(() => _replaced = true),
              ),
            ),
            const SizedBox(width: 8),
            Expanded(
              child: _Answer(
                key: const ValueKey('replaced-no'),
                label: 'No, office decides',
                selected: _replaced == false,
                onTap: () => setState(() => _replaced = false),
              ),
            ),
          ],
        ),
        if (_replaced != null) ...[
          const SizedBox(height: 8),
          Padding(
            padding: const EdgeInsets.symmetric(horizontal: 4),
            child: Text(
              _replaced!
                  ? 'The same products and quantities come off your van.'
                  : 'The office will decide whether the shop gets a credit or a replacement.',
              style: const TextStyle(fontSize: 12.5, color: AppColors.textMuted),
            ),
          ),
        ],
      ],
    );
  }

  Widget _saveBar(BuildContext context) => DecoratedBox(
        decoration: const BoxDecoration(
          color: AppColors.surface,
          border: Border(top: BorderSide(color: AppColors.border)),
        ),
        child: SafeArea(
          child: Padding(
            padding: const EdgeInsets.fromLTRB(16, 12, 12, 12),
            child: Row(
              children: [
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    mainAxisSize: MainAxisSize.min,
                    children: [
                      const Text('Packets collected', style: TextStyle(color: AppColors.textMuted)),
                      Text(quantityLabel(_total), style: Theme.of(context).textTheme.headlineSmall),
                    ],
                  ),
                ),
                SizedBox(
                  width: 170,
                  child: FilledButton.icon(
                    key: const ValueKey('save-return'),
                    // Closed while saving: two taps would be two returns with two client ids.
                    onPressed: _canSave ? _save : null,
                    icon: _saving
                        ? const SizedBox(
                            height: 18,
                            width: 18,
                            child: CircularProgressIndicator(strokeWidth: 2, color: Colors.white))
                        : const Icon(Icons.check, size: 18),
                    label: const Text('Save return'),
                  ),
                ),
              ],
            ),
          ),
        ),
      );

  Future<void> _save() async {
    final lines = _lines;
    final total = _total;
    final replaced = _replaced!;

    setState(() => _saving = true);

    try {
      await ref.read(salesRepositoryProvider).recordReturn(
            customerId: widget.shop.id,
            customerName: widget.shop.name,
            lines: lines,
            replacedFromVan: replaced,
            branchId: _branch?.id,
          );

      unawaitedSync(ref);

      if (!mounted) return;

      Navigator.of(context).pop();
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text('${quantityLabel(total)} packets recorded'
              '${replaced ? ', replaced from the van' : ' for the office to decide'}.'),
        ),
      );
    } finally {
      if (mounted) setState(() => _saving = false);
    }
  }
}

/// One of two answers. Neither starts selected, so saving needs a deliberate choice.
class _Answer extends StatelessWidget {
  const _Answer({required this.label, required this.selected, required this.onTap, super.key});

  final String label;
  final bool selected;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) => selected
      ? FilledButton(onPressed: onTap, child: Text(label))
      : OutlinedButton(onPressed: onTap, child: Text(label));
}
