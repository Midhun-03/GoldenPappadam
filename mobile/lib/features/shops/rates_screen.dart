import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../app.dart';
import '../../core/theme.dart';
import '../../core/widgets.dart';
import '../../data/local/database.dart';
import 'rate_lines.dart';

/// Asks the office to change what [shop] pays - one product card per rate, the same cards as a bill.
/// Opened from the shop page, with [product] already chosen when the salesperson tapped one of the
/// shop's rates.
///
/// A salesperson sets rates only when adding a new shop (CLAUDE.md §4 "Rate-change approval"). This
/// sends requests: the shop keeps paying what it pays now, and bills use that, until the office
/// approves - then the next sync brings the new rate. A new request for a product replaces one still
/// waiting.
class RatesScreen extends ConsumerStatefulWidget {
  const RatesScreen({required this.shop, this.product, super.key});

  final CachedCustomer shop;
  final CachedProduct? product;

  @override
  ConsumerState<RatesScreen> createState() => _RatesScreenState();
}

class _RatesScreenState extends ConsumerState<RatesScreen> {
  late final _lines = RateLinesController(first: widget.product);
  final _reason = TextEditingController();
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
    _reason.dispose();
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
              const Text('RATE CHANGE REQUEST', style: kEyebrowStyle),
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
                      'The office decides. Until they approve, this shop keeps paying what it pays now.',
                      style: TextStyle(color: AppColors.textMuted),
                    ),
                  ),
                  RateLinesEditor(controller: _lines, products: _products, currentRates: _current),
                  const SectionHeader('Why'),
                  AppCard(
                    child: TextField(
                      key: const ValueKey('rate-request-reason'),
                      controller: _reason,
                      maxLength: 300,
                      maxLines: 2,
                      decoration: const InputDecoration(
                        hintText: 'Optional - what the shop said, for the office',
                        counterText: '',
                      ),
                    ),
                  ),
                ],
              ),
        bottomNavigationBar: SafeArea(
          child: Padding(
            padding: const EdgeInsets.fromLTRB(12, 8, 12, 12),
            child: FilledButton.icon(
              key: const ValueKey('send-rate-request'),
              // Closed while sending: a second tap would be a second set of requests.
              onPressed: _saving || _lines.entries.isEmpty ? null : _save,
              icon: const Icon(Icons.send, size: 18),
              label: const Text('Send to the office'),
            ),
          ),
        ),
      );

  Future<void> _save() async {
    setState(() => _saving = true);

    try {
      final entries = _lines.entries;
      await ref.read(shopsRepositoryProvider).requestRates(widget.shop, entries, reason: _reason.text);
      unawaitedSync(ref);

      if (!mounted) return;
      Navigator.of(context).pop(true);
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text('${entries.length == 1 ? 'Request' : '${entries.length} requests'} sent to the office. '
              'The rate changes when they approve.'),
        ),
      );
    } finally {
      if (mounted) setState(() => _saving = false);
    }
  }
}
