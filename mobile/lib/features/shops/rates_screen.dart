import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../app.dart';
import '../../core/theme.dart';
import '../../data/local/database.dart';
import 'rate_lines.dart';

/// Sets what [shop] pays, one product card per rate. Opened from the shop page, with [product]
/// already chosen when the salesperson tapped one of the shop's rates.
///
/// A rate applies from the next bill, at every branch; a bill already made keeps its price. The
/// office sees each change with the salesperson's name and can override it. Removing a rate is the
/// office's call, so it is not offered here.
class RatesScreen extends ConsumerStatefulWidget {
  const RatesScreen({required this.shop, this.product, super.key});

  final CachedCustomer shop;
  final CachedProduct? product;

  @override
  ConsumerState<RatesScreen> createState() => _RatesScreenState();
}

class _RatesScreenState extends ConsumerState<RatesScreen> {
  late final _lines = RateLinesController(first: widget.product);
  List<CachedProduct> _products = const [];
  Map<String, double?> _current = const {};
  bool _loaded = false;
  bool _saving = false;

  @override
  void initState() {
    super.initState();
    _lines.addListener(() => setState(() {}));
    _load();
  }

  Future<void> _load() async {
    final db = ref.read(databaseProvider);
    final products = await db.allProducts();
    final current = await db.pricesFor(widget.shop.id);
    if (!mounted) return;

    setState(() {
      _products = products;
      _current = current;
      _loaded = true;
    });
  }

  @override
  void dispose() {
    _lines.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) => Scaffold(
        appBar: AppBar(
          toolbarHeight: 64,
          title: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            mainAxisSize: MainAxisSize.min,
            children: [
              const Text('RATES', style: kEyebrowStyle),
              Text(widget.shop.name, style: Theme.of(context).textTheme.titleLarge),
            ],
          ),
        ),
        body: !_loaded
            ? const Center(child: CircularProgressIndicator())
            : ListView(
                padding: const EdgeInsets.fromLTRB(12, 8, 12, 24),
                children: [
                  const Padding(
                    padding: EdgeInsets.fromLTRB(4, 0, 4, 10),
                    child: Text(
                      'From the next bill, at every branch. The office sees every change.',
                      style: TextStyle(color: AppColors.textMuted),
                    ),
                  ),
                  RateLinesEditor(controller: _lines, products: _products, currentRates: _current),
                ],
              ),
        bottomNavigationBar: SafeArea(
          child: Padding(
            padding: const EdgeInsets.fromLTRB(12, 8, 12, 12),
            child: FilledButton.icon(
              key: const ValueKey('save-rates'),
              onPressed: _saving || _lines.entries.isEmpty ? null : _save,
              icon: const Icon(Icons.check, size: 18),
              label: const Text('Save rates'),
            ),
          ),
        ),
      );

  Future<void> _save() async {
    setState(() => _saving = true);

    try {
      final entries = _lines.entries;
      await ref.read(shopsRepositoryProvider).setRates(widget.shop, entries);
      unawaitedSync(ref);

      if (!mounted) return;
      Navigator.of(context).pop(true);
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text('${entries.length} rate${entries.length == 1 ? '' : 's'} saved.')),
      );
    } finally {
      if (mounted) setState(() => _saving = false);
    }
  }
}
