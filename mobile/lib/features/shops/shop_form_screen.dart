import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../app.dart';
import '../../core/theme.dart';
import '../../core/widgets.dart';
import '../../data/local/database.dart';
import '../../data/shops_repository.dart';
import 'rate_lines.dart';
import 'shop_screen.dart';

/// A shop the salesperson found, or new details for one they have.
///
/// The existing shops are searched as the name is typed: an exact match cannot be added again and
/// is offered to open instead, and a name containing a known shop's ("Danya Supermarket Coimbatore")
/// is pointed at that shop, to be added as its branch. A new shop can be given its rates here
/// straight away, as product cards - saved with the shop, so it can be billed at once.
///
/// Deliberately absent: an opening balance (a shop the salesperson found owes nothing yet), GSTIN
/// and notes (the office's), and anything that closes a shop.
class ShopFormScreen extends ConsumerStatefulWidget {
  const ShopFormScreen({this.shop, this.initialName, super.key});

  /// Null for a new shop.
  final CachedCustomer? shop;

  /// What was typed in the search, to start the new shop's name from.
  final String? initialName;

  @override
  ConsumerState<ShopFormScreen> createState() => _ShopFormScreenState();
}

class _ShopFormScreenState extends ConsumerState<ShopFormScreen> {
  final _form = GlobalKey<FormState>();
  late final _name = TextEditingController(text: widget.shop?.name ?? widget.initialName ?? '');
  late final _contact = TextEditingController(text: widget.shop?.contactPerson ?? '');
  late final _phone = TextEditingController(text: widget.shop?.phone ?? '');
  late final _address = TextEditingController(text: widget.shop?.address ?? '');
  late bool _hasBranches = widget.shop?.hasMultipleBranches ?? false;

  List<CachedProduct> _products = const [];
  final _rates = RateLinesController();
  int _branchCount = 0;

  /// What the typed name matches among the shops the phone knows.
  CachedCustomer? _sameShop;
  CachedCustomer? _parentShop;
  bool _saving = false;
  String? _error;

  bool get _isNew => widget.shop == null;

  @override
  void initState() {
    super.initState();
    _rates.addListener(() => setState(() {}));
    _name.addListener(_checkName);
    _load();
    _checkName();
  }

  Future<void> _checkName() async {
    final repository = ref.read(shopsRepositoryProvider);
    final typed = _name.text;
    final same = await repository.findByName(typed);
    final parent = same == null ? await repository.likelyParentOf(typed) : null;

    if (!mounted || typed != _name.text) return;
    setState(() {
      // Editing a shop keeps its own name without calling it a duplicate of itself.
      _sameShop = same?.id == widget.shop?.id ? null : same;
      _parentShop = parent?.id == widget.shop?.id ? null : parent;
    });
  }

  Future<void> _load() async {
    final db = ref.read(databaseProvider);
    final products = _isNew ? await db.allProducts() : const <CachedProduct>[];
    final branches = _isNew ? const <CachedBranch>[] : await db.branchesFor(widget.shop!.id);

    if (!mounted) return;
    setState(() {
      _products = products;
      _branchCount = branches.length;
    });
  }

  @override
  void dispose() {
    for (final controller in [_name, _contact, _phone, _address]) {
      controller.dispose();
    }
    _rates.dispose();
    super.dispose();
  }

  ShopDetails get _details => ShopDetails(
        name: _name.text,
        contactPerson: _contact.text,
        phone: _phone.text,
        address: _address.text,
        hasMultipleBranches: _hasBranches,
      );


  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: Text(_isNew ? 'New shop' : 'Edit shop')),
      body: Form(
        key: _form,
        child: ListView(
          padding: const EdgeInsets.fromLTRB(12, 8, 12, 24),
          children: [
            AppCard(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  TextFormField(
                    key: const ValueKey('shop-name'),
                    controller: _name,
                    textCapitalization: TextCapitalization.words,
                    maxLength: 150,
                    decoration: const InputDecoration(labelText: 'Shop name *', counterText: ''),
                    validator: (value) => (value ?? '').trim().isEmpty ? 'Enter the shop name' : null,
                  ),
                  if (_sameShop != null)
                    _NameHint(
                      key: const ValueKey('same-shop'),
                      text: '${_sameShop!.name} is already a shop.',
                      action: 'Open ${_sameShop!.name}',
                      onPressed: () => _openInstead(_sameShop!),
                    )
                  else if (_parentShop != null)
                    _NameHint(
                      key: const ValueKey('parent-shop'),
                      text: 'Is this a branch of ${_parentShop!.name}? '
                          'Add it as a branch there, not as a new shop.',
                      action: 'Open ${_parentShop!.name}',
                      onPressed: () => _openInstead(_parentShop!),
                    ),
                  const SizedBox(height: 12),
                  TextFormField(
                    key: const ValueKey('shop-contact'),
                    controller: _contact,
                    textCapitalization: TextCapitalization.words,
                    maxLength: 100,
                    decoration: const InputDecoration(labelText: 'Contact person', counterText: ''),
                  ),
                  const SizedBox(height: 12),
                  TextFormField(
                    key: const ValueKey('shop-phone'),
                    controller: _phone,
                    keyboardType: TextInputType.phone,
                    maxLength: 20,
                    decoration: const InputDecoration(labelText: 'Phone', counterText: ''),
                  ),
                  const SizedBox(height: 12),
                  TextFormField(
                    key: const ValueKey('shop-address'),
                    controller: _address,
                    maxLength: 300,
                    maxLines: 2,
                    decoration: const InputDecoration(labelText: 'Address', counterText: ''),
                  ),
                  const SizedBox(height: 4),
                  SwitchListTile(
                    key: const ValueKey('shop-has-branches'),
                    contentPadding: EdgeInsets.zero,
                    title: const Text('Has several branches'),
                    subtitle: const Text('Like Danya Supermarket with shops in Kundara and Coimbatore. '
                        'Each bill then names its branch.'),
                    value: _hasBranches,
                    // Branches still open cannot be orphaned: closing them is the office's call.
                    onChanged: !_isNew && _hasBranches && _branchCount > 0
                        ? null
                        : (value) => setState(() => _hasBranches = value),
                  ),
                  if (!_isNew && _hasBranches && _branchCount > 0)
                    const Text(
                      'This shop has branches, so this stays on. Ask the office to close them first.',
                      style: TextStyle(fontSize: 12, color: AppColors.textMuted),
                    ),
                ],
              ),
            ),
            if (_isNew && _products.isNotEmpty) ...[
              const SectionHeader('Rates for this shop'),
              const Padding(
                padding: EdgeInsets.fromLTRB(4, 0, 4, 10),
                child: Text(
                  'What the shop agreed to pay. A product with no rate here pays the standard price.',
                  style: TextStyle(fontSize: 12.5, color: AppColors.textMuted),
                ),
              ),
              RateLinesEditor(
                controller: _rates,
                products: _products,
                currentRates: {for (final p in _products) p.id: p.defaultPrice},
              ),
            ],
            if (_error != null) ...[
              const SizedBox(height: 12),
              Text(_error!, style: const TextStyle(color: AppColors.danger)),
            ],
          ],
        ),
      ),
      bottomNavigationBar: SafeArea(
        child: Padding(
          padding: const EdgeInsets.fromLTRB(12, 8, 12, 12),
          child: FilledButton.icon(
            key: const ValueKey('save-shop'),
            onPressed: _saving || _sameShop != null ? null : _save,
            icon: const Icon(Icons.check, size: 18),
            label: Text(_isNew ? 'Add shop' : 'Save changes'),
          ),
        ),
      ),
    );
  }

  Future<void> _save() async {
    if (!_form.currentState!.validate()) return;

    setState(() {
      _saving = true;
      _error = null;
    });

    final repository = ref.read(shopsRepositoryProvider);

    try {
      if (_isNew) {
        final id = await repository.createShop(_details, rates: _rates.entries);
        unawaitedSync(ref);
        if (!mounted) return;

        // Straight to the shop's page, where the next thing - a branch, a bill - is one tap away.
        Navigator.of(context).pushReplacement(appRoute<void>((_) => ShopScreen(shopId: id)));
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text('${_name.text.trim()} added.')),
        );
      } else {
        await repository.updateShop(widget.shop!, _details);
        unawaitedSync(ref);
        if (!mounted) return;

        Navigator.of(context).pop();
      }
    } on StateError catch (error) {
      setState(() => _error = error.message);
    } finally {
      if (mounted) setState(() => _saving = false);
    }
  }

  void _openInstead(CachedCustomer shop) =>
      Navigator.of(context).pushReplacement(appRoute<void>((_) => ShopScreen(shopId: shop.id)));
}

/// Under the name: the shop already exists, or looks like a branch of one that does.
class _NameHint extends StatelessWidget {
  const _NameHint({required this.text, required this.action, required this.onPressed, super.key});

  final String text;
  final String action;
  final VoidCallback onPressed;

  @override
  Widget build(BuildContext context) => Container(
        margin: const EdgeInsets.only(top: 8),
        padding: const EdgeInsets.fromLTRB(12, 10, 4, 4),
        decoration: BoxDecoration(
          color: AppColors.warningSoft,
          borderRadius: BorderRadius.circular(AppRadius.sm),
        ),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(text, style: const TextStyle(fontWeight: FontWeight.w600)),
            Align(
              alignment: Alignment.centerRight,
              child: TextButton(onPressed: onPressed, child: Text(action)),
            ),
          ],
        ),
      );
}
