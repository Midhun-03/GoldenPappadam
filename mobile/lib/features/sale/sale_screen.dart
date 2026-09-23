import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../app.dart';
import '../../core/money.dart';
import '../../core/searchable_dropdown.dart';
import '../../core/theme.dart';
import '../../core/widgets.dart';
import '../../data/local/database.dart';
import '../../data/sales_repository.dart';

/// A new bill built as three short steps on one page: pick the shop, set a quantity on whatever
/// it is priced for, then say how it was paid. Save records it straight into the outbox.
///
/// The price sits on each row as plain text. There is no field to edit it, because the office
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

enum _PaymentPlan { credit, paid, partPaid }

class _SaleScreenState extends ConsumerState<SaleScreen> {
  CachedCustomer? _shop;
  List<CachedCustomer> _shops = const [];
  List<CachedProduct> _products = const [];
  Map<String, double?> _prices = const {};
  String? _pricesAsOf;
  bool _loaded = false;
  bool _saving = false;

  List<CachedBranch> _branches = const [];
  CachedBranch? _branch;

  final Map<String, TextEditingController> _qtyControllers = {};
  final _productSearch = TextEditingController();
  String _productFilter = '';

  _PaymentPlan _plan = _PaymentPlan.credit;
  final _partialAmount = TextEditingController();
  String _method = 'Cash';

  @override
  void initState() {
    super.initState();
    _shop = widget.shop;
    _productSearch.addListener(() => setState(() => _productFilter = _productSearch.text));
    _partialAmount.addListener(() => setState(() {}));
    _load();
  }

  @override
  void dispose() {
    for (final controller in _qtyControllers.values) {
      controller.dispose();
    }
    _productSearch.dispose();
    _partialAmount.dispose();
    super.dispose();
  }

  Future<void> _load() async {
    final db = ref.read(databaseProvider);
    final shops = await db.allCustomers();
    final recent = await db.recentShopNames();
    final products = await db.allProducts();
    final asOf = await ref.read(syncProvider).pricesAsOf;
    final prices = _shop == null ? const <String, double?>{} : await db.pricesFor(_shop!.id);
    final branches = _shop == null ? const <CachedBranch>[] : await db.branchesFor(_shop!.id);

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
      _branches = branches;
      _loaded = true;
    });
  }

  Future<void> _chooseShop(CachedCustomer? shop) async {
    if (shop == null || shop.id == _shop?.id) return;

    final db = ref.read(databaseProvider);
    final prices = await db.pricesFor(shop.id);
    final branches = shop.hasMultipleBranches ? await db.branchesFor(shop.id) : const <CachedBranch>[];
    if (!mounted) return;

    // A new shop means a fresh bill: the old quantities were against the old shop's prices, and a
    // branch chosen for the old shop must never silently attach to this one.
    for (final controller in _qtyControllers.values) {
      controller.dispose();
    }

    setState(() {
      _shop = shop;
      _prices = prices;
      _branches = branches;
      _branch = null;
      _qtyControllers.clear();
      _productSearch.clear();
      _productFilter = '';
      _plan = _PaymentPlan.credit;
      _partialAmount.clear();
    });
  }

  bool get _needsBranch => _shop?.hasMultipleBranches ?? false;

  bool get _branchIsValid => !_needsBranch || _branch != null;

  /// Only what this shop has a price for. Everything else would be a guess.
  List<CachedProduct> get _sellable =>
      _products.where((product) => _prices[product.id] != null).toList();

  List<CachedProduct> get _visibleProducts {
    final needle = _productFilter.trim().toLowerCase();
    if (needle.isEmpty) return _sellable;

    return _sellable.where((p) => p.name.toLowerCase().contains(needle)).toList();
  }

  TextEditingController _controllerFor(String productId) =>
      _qtyControllers.putIfAbsent(productId, TextEditingController.new);

  double _quantityOf(CachedProduct product) =>
      double.tryParse(_qtyControllers[product.id]?.text ?? '') ?? 0;

  /// Every priced product with a quantity on it. Nothing here can name the same product twice,
  /// because there is exactly one row per product rather than a row per line.
  List<SaleLine> get _lines => [
        for (final product in _sellable)
          if (_quantityOf(product) > 0)
            SaleLine(
              productId: product.id,
              productName: product.name,
              quantity: _quantityOf(product),
              unitPrice: _prices[product.id] ?? 0,
            ),
      ];

  double get _total => _lines.fold(0, (sum, line) => sum + line.lineTotal);

  double get _partialAmountValue => double.tryParse(_partialAmount.text) ?? 0;

  bool get _planIsValid => _plan != _PaymentPlan.partPaid || _partialAmountValue > 0;

  bool get _canSave =>
      _shop != null && _lines.isNotEmpty && _planIsValid && _branchIsValid && !_saving;

  double get _amountPaid => switch (_plan) {
        _PaymentPlan.credit => 0,
        _PaymentPlan.paid => _total,
        _PaymentPlan.partPaid => _partialAmountValue,
      };

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        toolbarHeight: 64,
        title: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          mainAxisSize: MainAxisSize.min,
          children: [
            const Text('SALE · STEP BY STEP', style: kEyebrowStyle),
            Text('New Bill', style: Theme.of(context).textTheme.titleLarge),
          ],
        ),
      ),
      body: !_loaded
          ? const Center(child: CircularProgressIndicator())
          : FadeIn(
              child: ListView(
                padding: const EdgeInsets.fromLTRB(12, 4, 12, 24),
                children: [
                  const NumberedStepHeader(1, 'Select shop'),
                  _shopCard(context),
                  const NumberedStepHeader(2, 'Add products', caption: 'Price is read-only'),
                  _productsCard(context),
                  if (_shop != null && _sellable.isNotEmpty) ...[
                    const NumberedStepHeader(3, 'Payment'),
                    _paymentCard(context),
                  ],
                ],
              ),
            ),
      bottomNavigationBar: _loaded
          ? _Total(total: _total, saving: _saving, onSave: _canSave ? _save : null)
          : null,
    );
  }

  Widget _shopCard(BuildContext context) {
    final shop = _shop;

    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        SearchableDropdown<CachedCustomer>(
          key: const ValueKey('shop'),
          hintText: 'Search or select shop',
          helperText: shop == null ? 'Required before saving' : null,
          selected: _shops.where((s) => s.id == shop?.id).firstOrNull,
          searchTextOf: (s) => s.phone ?? '',
          onSelected: _chooseShop,
          textStyle: const TextStyle(fontWeight: FontWeight.w700),
          leading: Container(
            width: 34,
            height: 34,
            alignment: Alignment.center,
            decoration: BoxDecoration(
              color: AppColors.charcoal,
              borderRadius: BorderRadius.circular(AppRadius.sm),
            ),
            child: const Icon(Icons.storefront, color: Colors.white, size: 18),
          ),
          entries: [
            for (final s in _shops)
              DropdownMenuEntry(
                value: s,
                label: s.name,
                trailingIcon: s.balance > 0
                    ? Text('owes ${money(s.balance)}',
                        style: const TextStyle(fontSize: 12, color: AppColors.danger))
                    : null,
              ),
          ],
        ),
        if (shop != null && shop.balance > 0) ...[
          const SizedBox(height: 10),
          Row(
            children: [
              const Icon(Icons.error_outline, size: 16, color: AppColors.danger),
              const SizedBox(width: 6),
              Expanded(
                child: Text(
                  'This shop already owes ${money(shop.balance)}.',
                  style: const TextStyle(fontSize: 12.5, color: AppColors.danger),
                ),
              ),
            ],
          ),
        ],
        if (_needsBranch) ...[
          const SizedBox(height: 10),
          SearchableDropdown<CachedBranch>(
            key: const ValueKey('branch'),
            hintText: 'Search or select branch',
            helperText: _branch == null ? 'Required before saving' : null,
            selected: _branches.where((b) => b.id == _branch?.id).firstOrNull,
            searchTextOf: (b) => b.location ?? '',
            onSelected: (branch) => setState(() => _branch = branch),
            entries: [
              for (final b in _branches) DropdownMenuEntry(value: b, label: b.name),
            ],
          ),
        ],
      ],
    );
  }

  Widget _productsCard(BuildContext context) {
    final shop = _shop;

    if (shop == null) {
      return const AppCard(
        child: Text('Choose the shop first - what it pays decides what can go on the bill.',
            style: TextStyle(color: AppColors.textMuted)),
      );
    }

    if (_sellable.isEmpty) {
      return const AppCard(child: _NoPrices());
    }

    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        if (_sellable.length > 5) ...[
          TextField(
            controller: _productSearch,
            decoration: InputDecoration(
              hintText: 'Search products',
              prefixIcon: const Icon(Icons.search),
              suffixIcon: _productFilter.isEmpty
                  ? null
                  : IconButton(icon: const Icon(Icons.clear), onPressed: _productSearch.clear),
            ),
          ),
          const SizedBox(height: 10),
        ],
        AppCard(
          padding: const EdgeInsets.symmetric(horizontal: 14),
          child: _visibleProducts.isEmpty
              ? const Padding(
                  padding: EdgeInsets.symmetric(vertical: 16),
                  child: Text('No product by that name', style: TextStyle(color: AppColors.textMuted)),
                )
              : Column(
                  children: [
                    for (final product in _visibleProducts) ...[
                      _productRow(product),
                      if (product != _visibleProducts.last) const Divider(height: 22),
                    ],
                  ],
                ),
        ),
      ],
    );
  }

  Widget _productRow(CachedProduct product) {
    final price = _prices[product.id] ?? 0;

    return Padding(
      key: ValueKey('product-row-${product.id}'),
      padding: const EdgeInsets.symmetric(vertical: 6),
      child: Row(
        children: [
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(product.name, style: const TextStyle(fontWeight: FontWeight.w700)),
                const SizedBox(height: 2),
                Text('${money(price)} / ${product.unitCode.toLowerCase()}',
                    style: const TextStyle(fontSize: 12.5, color: AppColors.textMuted)),
              ],
            ),
          ),
          const SizedBox(width: 10),
          QuantityStepper(
            controller: _controllerFor(product.id),
            fieldKey: ValueKey('qty-${product.id}'),
            onChanged: (_) => setState(() {}),
          ),
        ],
      ),
    );
  }

  Widget _paymentCard(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        ChoiceRow<_PaymentPlan>(
          value: _plan,
          onChanged: (plan) => setState(() => _plan = plan),
          options: const [
            (_PaymentPlan.credit, 'Credit'),
            (_PaymentPlan.paid, 'Paid'),
            (_PaymentPlan.partPaid, 'Part Paid'),
          ],
        ),
        if (_plan != _PaymentPlan.credit) ...[
          const SizedBox(height: 12),
          AppCard(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                if (_plan == _PaymentPlan.partPaid) ...[
                  TextField(
                    key: const ValueKey('partial-amount'),
                    controller: _partialAmount,
                    autofocus: true,
                    keyboardType: const TextInputType.numberWithOptions(decimal: true),
                    decoration: const InputDecoration(labelText: 'Amount received', prefixText: '₹ '),
                  ),
                  const SizedBox(height: 12),
                ],
                SegmentedButton<String>(
                  segments: const [
                    ButtonSegment(value: 'Cash', label: Text('Cash')),
                    ButtonSegment(value: 'UPI', label: Text('UPI')),
                  ],
                  selected: {_method},
                  onSelectionChanged: (values) => setState(() => _method = values.first),
                ),
              ],
            ),
          ),
        ],
      ],
    );
  }

  Future<void> _save() async {
    final shop = _shop!;
    final lines = _lines;
    final total = _total;
    final amountPaid = _amountPaid;
    final method = _method;

    setState(() => _saving = true);

    try {
      await ref.read(salesRepositoryProvider).recordSale(
            customerId: shop.id,
            customerName: shop.name,
            lines: lines,
            amountPaid: amountPaid,
            paymentMethod: method,
            pricesAsOf: _pricesAsOf,
            branchId: _branch?.id,
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
  Widget build(BuildContext context) => DecoratedBox(
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
                      const Text('Total amount', style: TextStyle(color: AppColors.textMuted)),
                      AnimatedAmount(
                        money(total),
                        style: Theme.of(context).textTheme.headlineSmall,
                      ),
                    ],
                  ),
                ),
                SizedBox(
                  width: 160,
                  child: FilledButton.icon(
                    onPressed: saving ? null : onSave,
                    icon: saving
                        ? const SizedBox(
                            height: 18,
                            width: 18,
                            child: CircularProgressIndicator(strokeWidth: 2, color: Colors.white))
                        : const Icon(Icons.check, size: 18),
                    label: const Text('Save Bill'),
                  ),
                ),
              ],
            ),
          ),
        ),
      );
}

class _NoPrices extends StatelessWidget {
  const _NoPrices();

  @override
  Widget build(BuildContext context) => const EmptyState(
        icon: Icons.sell_outlined,
        title: 'No prices for this shop yet',
        message: 'The office sets what each shop pays. Ask them to add it, then sync.',
      );
}
