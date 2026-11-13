import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../app.dart';
import '../../core/money.dart';
import '../../core/product_line_card.dart';
import '../../core/searchable_dropdown.dart';
import '../../core/theme.dart';
import '../../core/widgets.dart';
import '../../data/local/database.dart';
import '../../data/sales_repository.dart';

/// A new bill built as three short steps on one page: pick the shop, add a line per product -
/// chosen from a dropdown, with a quantity - then say how it was paid. Save records it straight
/// into the outbox.
///
/// The price sits on each row as plain text. There is no field to edit it, because the office
/// decides what each shop pays and the salesperson carries that decision rather than making one.
/// A product the office has never priced for the chosen shop is not offered at all, which is a
/// better failure than an invented number.
///
/// Charging less is a discount on the whole bill, behind a quiet "Add discount" row because it is
/// rare. It is in rupees, needs a reason the office will read, and cannot go over the share of the
/// bill the office allows - the bill cannot be saved until it fits.
///
/// Opened from Home with no shop, or from a shop's page with that shop already chosen.
class SaleScreen extends ConsumerStatefulWidget {
  const SaleScreen({this.shop, super.key});

  final CachedCustomer? shop;

  @override
  ConsumerState<SaleScreen> createState() => _SaleScreenState();
}

enum _PaymentPlan { credit, paid, partPaid }

/// One line of the bill while it is being built: a product, once chosen, and its quantity.
class _BillLine {
  CachedProduct? product;
  final quantity = TextEditingController();
}

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

  List<_BillLine> _billLines = [_BillLine()];

  _PaymentPlan _plan = _PaymentPlan.credit;
  final _partialAmount = TextEditingController();
  String _method = 'Cash';

  /// The most that may come off a bill, as a percentage of it. Zero: no discount is offered.
  double _discountCap = 0;
  bool _discountOpen = false;
  final _discount = TextEditingController();
  final _discountReason = TextEditingController();

  @override
  void initState() {
    super.initState();
    _shop = widget.shop;
    _partialAmount.addListener(() => setState(() {}));
    _discount.addListener(() => setState(() {}));
    _discountReason.addListener(() => setState(() {}));
    _load();
  }

  @override
  void dispose() {
    for (final line in _billLines) {
      line.quantity.dispose();
    }
    _partialAmount.dispose();
    _discount.dispose();
    _discountReason.dispose();
    super.dispose();
  }

  Future<void> _load() async {
    final db = ref.read(databaseProvider);
    final shops = await db.allCustomers();
    final recent = await db.recentShopNames();
    final products = await db.allProducts();
    final asOf = await ref.read(syncProvider).pricesAsOf;
    final discountCap = await ref.read(syncProvider).discountCapPercent;
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
      _discountCap = discountCap;
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

    // A new shop means a fresh bill: the old lines were against the old shop's prices, and a
    // branch chosen for the old shop must never silently attach to this one.
    for (final line in _billLines) {
      line.quantity.dispose();
    }

    setState(() {
      _shop = shop;
      _prices = prices;
      _branches = branches;
      _branch = null;
      _billLines = [_BillLine()];
      _plan = _PaymentPlan.credit;
      _partialAmount.clear();
      _closeDiscount();
    });
  }

  /// A dropdown keeps the cursor, and the keyboard, after something is picked from it. Letting go
  /// puts the keyboard away so the next step is in view, and lets the part-payment amount take
  /// the cursor when it appears.
  void _doneTyping() => FocusManager.instance.primaryFocus?.unfocus();

  bool get _needsBranch => _shop?.hasMultipleBranches ?? false;

  bool get _branchIsValid => !_needsBranch || _branch != null;

  /// Only what this shop has a price for. Everything else would be a guess.
  List<CachedProduct> get _sellable =>
      _products.where((product) => _prices[product.id] != null).toList();

  double _quantityOf(_BillLine line) => double.tryParse(line.quantity.text) ?? 0;

  /// Every line with a product and a quantity. A product can be on one line only, because the
  /// dropdowns never offer what another line already has.
  List<SaleLine> get _lines => [
        for (final line in _billLines)
          if (line.product != null && _quantityOf(line) > 0)
            SaleLine(
              productId: line.product!.id,
              productName: line.product!.name,
              quantity: _quantityOf(line),
              unitPrice: _prices[line.product!.id] ?? 0,
            ),
      ];

  /// What this line may choose: the shop's priced products that no other line has taken.
  List<CachedProduct> _choicesFor(_BillLine line) {
    final taken = {
      for (final other in _billLines)
        if (other != line && other.product != null) other.product!.id,
    };

    return _sellable.where((product) => !taken.contains(product.id)).toList();
  }

  bool get _canAddLine => _billLines.every((line) => line.product != null) &&
      _billLines.length < _sellable.length;

  void _addLine() => setState(() => _billLines = [..._billLines, _BillLine()]);

  void _removeLine(_BillLine line) {
    setState(() => _billLines = _billLines.where((other) => other != line).toList());
    line.quantity.dispose();
  }

  /// The lines added up, before any discount. The discount cap is a share of this.
  double get _subtotal => _lines.fold(0, (sum, line) => sum + line.lineTotal);

  double get _discountValue => _discountOpen ? (double.tryParse(_discount.text) ?? 0) : 0;

  /// The cap in rupees for the bill as it stands, to the paisa and never rounded up - the office
  /// compares exactly, and a paisa over would be refused after the goods were handed over.
  double get _maxDiscount => (_subtotal * _discountCap + 1e-6).floorToDouble() / 100;

  bool get _discountTooBig => _discountValue > _maxDiscount + 1e-9;

  bool get _discountNeedsReason => _discountValue > 0 && _discountReason.text.trim().isEmpty;

  /// A discount that no longer fits - a line was taken off after it was typed - blocks the save
  /// rather than being lowered quietly: the shop may already have been told the figure.
  bool get _discountIsValid => _discountValue >= 0 && !_discountTooBig && !_discountNeedsReason;

  void _closeDiscount() {
    _discountOpen = false;
    _discount.clear();
    _discountReason.clear();
  }

  /// What the shop is asked for: the lines less the discount. A discount over the cap cannot be
  /// saved, so it is not taken off the figure either - a total the shop can never be asked for
  /// would only mislead.
  double get _total => _discountTooBig ? _subtotal : _subtotal - _discountValue;

  double get _partialAmountValue => double.tryParse(_partialAmount.text) ?? 0;

  bool get _planIsValid => _plan != _PaymentPlan.partPaid || _partialAmountValue > 0;

  bool get _canSave =>
      _shop != null &&
      _lines.isNotEmpty &&
      _planIsValid &&
      _branchIsValid &&
      _discountIsValid &&
      !_saving;

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
          ? const SizedBox.expand()
          // One short entrance for the page, not one per card: this page is opened dozens of
          // times a day, and anything longer would be in the way.
          : FadeIn(
              child: ListView(
                padding: const EdgeInsets.fromLTRB(16, 0, 16, 28),
                children: [
                  const NumberedStepHeader(1, 'Select shop'),
                  _shopCard(context),
                  const NumberedStepHeader(2, 'Products', caption: 'Price is read-only'),
                  _productsCard(context),
                  if (_shop != null && _sellable.isNotEmpty && _discountCap > 0) _discountSection(context),
                  if (_shop != null && _sellable.isNotEmpty) ...[
                    const NumberedStepHeader(3, 'Payment'),
                    _paymentCard(context),
                  ],
                ],
              ),
            ),
      // Two taps would be two sales, each with its own client request id, which the server could
      // not tell apart - so the button closes while the first is saving.
      bottomNavigationBar: _loaded
          ? BottomActionBar(
              label: 'Total amount',
              amount: money(_total),
              actionLabel: 'Save Bill',
              busy: _saving,
              onPressed: _canSave ? _save : null,
            )
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
          onSelected: (shop) {
            _doneTyping();
            _chooseShop(shop);
          },
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
        if (shop != null) ...[
          const SizedBox(height: 10),
          Align(alignment: Alignment.centerLeft, child: BillKindPill(gstin: shop.gstin)),
        ],
        if (shop != null && shop.balance > 0) ...[
          const SizedBox(height: 10),
          InlineNotice(
            'This shop already owes ${money(shop.balance)}.',
            tone: Tone.danger,
            icon: Icons.error_outline,
          ),
        ],
        if (_needsBranch) ...[
          const SizedBox(height: 12),
          SearchableDropdown<CachedBranch>(
            key: const ValueKey('branch'),
            hintText: 'Search or select branch',
            helperText: _branch == null ? 'Required before saving' : null,
            selected: _branches.where((b) => b.id == _branch?.id).firstOrNull,
            searchTextOf: (b) => b.location ?? '',
            onSelected: (branch) {
              _doneTyping();
              setState(() => _branch = branch);
            },
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
        for (final (index, line) in _billLines.indexed)
          // The object key keeps each line's dropdown and quantity with that line when another
          // is removed; the index key is what the tests and the screen reader count by.
          KeyedSubtree(
            key: ObjectKey(line),
            child: _lineCard(index, line),
          ),
        AddItemButton(buttonKey: const ValueKey('add-item'), onPressed: _canAddLine ? _addLine : null),
      ],
    );
  }

  Widget _lineCard(int index, _BillLine line) {
    final product = line.product;
    final price = product == null ? null : _prices[product.id];

    return ProductLineCard(
      name: 'bill',
      index: index,
      choices: _choicesFor(line),
      selected: product,
      onSelected: (chosen) {
        _doneTyping();
        setState(() => line.product = chosen);
      },
      field: QuantityField(
        controller: line.quantity,
        fieldKey: ValueKey('bill-qty-$index'),
        onChanged: (_) => setState(() {}),
      ),
      onRemove: _billLines.length == 1 ? null : () => _removeLine(line),
      // Shown, never typed: the office decides what each shop pays.
      besideField: ReadOnlyField(
        label: 'Price',
        value: price == null ? '—' : '${money(price)} / ${product!.unitCode.toLowerCase()}',
      ),
      footer: Row(
        children: [
          const Text('Line total', style: TextStyle(color: AppColors.textMuted)),
          const Spacer(),
          Text(money((price ?? 0) * _quantityOf(line)), style: const TextStyle(fontWeight: FontWeight.w700)),
        ],
      ),
    );
  }

  /// Closed, one quiet row: most bills have no discount. Open, the amount, the most allowed on
  /// this bill, and why.
  Widget _discountSection(BuildContext context) {
    if (!_discountOpen) {
      return Padding(
        padding: const EdgeInsets.only(top: 4),
        child: Align(
          alignment: Alignment.centerLeft,
          // Charcoal, not gold: gold is the one primary action on a screen, and that is Save Bill.
          child: TextButton.icon(
            key: const ValueKey('add-discount'),
            onPressed: () => setState(() => _discountOpen = true),
            style: TextButton.styleFrom(foregroundColor: AppColors.charcoal),
            icon: const Icon(Icons.sell_outlined, size: 18),
            label: const Text('Add discount'),
          ),
        ),
      );
    }

    final hasBill = _subtotal > 0;

    return Padding(
      padding: const EdgeInsets.only(top: 12),
      child: AppCard(
        padding: const EdgeInsets.all(AppSpacing.xl),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Row(
              children: [
                // Expanded, so a wide letter-spaced label gives way to the button on a narrow phone.
                const Expanded(child: Text('DISCOUNT', style: kEyebrowStyle)),
                TextButton(
                  key: const ValueKey('remove-discount'),
                  onPressed: () => setState(_closeDiscount),
                  style: TextButton.styleFrom(foregroundColor: AppColors.textMuted),
                  child: const Text('Remove'),
                ),
              ],
            ),
            const SizedBox(height: 4),
            TextField(
              key: const ValueKey('discount-amount'),
              controller: _discount,
              autofocus: true,
              keyboardType: const TextInputType.numberWithOptions(decimal: true),
              style: const TextStyle(fontSize: 20, fontWeight: FontWeight.w700, fontFeatures: kTabularFigures),
              // Over the cap, the line under the field says so in place of the hint. It sits against
              // the field on purpose: with the keyboard up there is no room for a notice further
              // down, and that is exactly when the figure is being typed.
              decoration: InputDecoration(
                prefixText: '₹ ',
                hintText: '0',
                helperText: hasBill
                    ? 'Up to ${money(_maxDiscount)} on this bill (${_percent(_discountCap)}%)'
                    : 'Add the products first',
                errorText: _discountTooBig && hasBill
                    ? 'The most you can take off this bill is ${money(_maxDiscount)}.'
                    : null,
                errorMaxLines: 2,
              ),
            ),
            const SizedBox(height: 16),
            const Text('WHY', style: kEyebrowStyle),
            const SizedBox(height: 8),
            TextField(
              key: const ValueKey('discount-reason'),
              controller: _discountReason,
              maxLength: 200,
              minLines: 1,
              maxLines: 3,
              textCapitalization: TextCapitalization.sentences,
              decoration: InputDecoration(
                hintText: 'Old stock, bulk order...',
                counterText: '',
                helperText: _discountNeedsReason
                    ? 'Needed with a discount. The office reads it; the shop does not see it.'
                    : 'The office reads it; the shop does not see it.',
              ),
            ),
          ],
        ),
      ),
    );
  }

  /// "20", "12.5" - a percentage without a trailing ".0".
  static String _percent(double value) =>
      value == value.roundToDouble() ? value.toStringAsFixed(0) : value.toString();

  Widget _paymentCard(BuildContext context) {
    final total = _total;
    final paid = _amountPaid;

    // What the choice above means in rupees, once there is a bill to apply it to. Silent when
    // more was handed over than the bill comes to: what happens to the rest is the office's call.
    final outcome = total <= 0 || paid > total
        ? null
        : switch (_plan) {
            _PaymentPlan.credit => '${money(total)} goes on credit.',
            _PaymentPlan.paid => '${money(total)} received now. Nothing on credit.',
            _PaymentPlan.partPaid when paid > 0 =>
              '${money(paid)} received now. ${money(total - paid)} stays on credit.',
            _PaymentPlan.partPaid => null,
          };

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
            padding: const EdgeInsets.all(AppSpacing.xl),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                if (_plan == _PaymentPlan.partPaid) ...[
                  const Text('AMOUNT RECEIVED', style: kEyebrowStyle),
                  const SizedBox(height: 8),
                  TextField(
                    key: const ValueKey('partial-amount'),
                    controller: _partialAmount,
                    autofocus: true,
                    keyboardType: const TextInputType.numberWithOptions(decimal: true),
                    style: const TextStyle(
                      fontSize: 20,
                      fontWeight: FontWeight.w700,
                      fontFeatures: kTabularFigures,
                    ),
                    decoration: const InputDecoration(prefixText: '₹ ', hintText: '0'),
                  ),
                  const SizedBox(height: 16),
                ],
                const Text('RECEIVED BY', style: kEyebrowStyle),
                const SizedBox(height: 8),
                // The same choice buttons as the row above, a full thumb tall, rather than a
                // second kind of control for the same kind of choice.
                ChoiceRow<String>(
                  value: _method,
                  onChanged: (method) => setState(() => _method = method),
                  options: const [('Cash', 'Cash'), ('UPI', 'UPI')],
                ),
              ],
            ),
          ),
        ],
        if (outcome != null) ...[
          const SizedBox(height: 12),
          Padding(
            padding: const EdgeInsets.symmetric(horizontal: 4),
            child: Text(
              outcome,
              style: const TextStyle(
                fontSize: 13.5,
                height: 1.35,
                color: AppColors.textMuted,
                fontFeatures: kTabularFigures,
              ),
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
    final discount = _discountValue;
    final discountReason = _discountReason.text;

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
            discountAmount: discount,
            discountReason: discount > 0 ? discountReason : null,
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

class _NoPrices extends StatelessWidget {
  const _NoPrices();

  @override
  Widget build(BuildContext context) => const EmptyState(
        icon: Icons.sell_outlined,
        title: 'No prices for this shop yet',
        message: "Set what this shop pays on its page (Shops, then 'Set a rate'), then come back to bill it.",
      );
}
