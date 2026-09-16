import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../app.dart';
import '../../core/money.dart';
import '../../data/local/database.dart';
import '../../data/remote/api_client.dart';
import '../../data/sales_repository.dart';
import '../sync/sync_views.dart';

/// What is on the van: taken this morning, delivered since, and what is left.
///
/// The salesman already writes this in a book - the packing unit says 500 were packed, he takes 350
/// and notes it down. This replaces the book, and nothing more: he can add what he took from the
/// warehouse onto his own van, and he cannot touch stock in any other way.
class VanScreen extends ConsumerStatefulWidget {
  const VanScreen({super.key});

  @override
  ConsumerState<VanScreen> createState() => _VanScreenState();
}

class _VanScreenState extends ConsumerState<VanScreen> {
  Map<String, dynamic>? _van;
  String? _error;
  bool _loading = true;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    setState(() => _loading = true);

    try {
      final van = await ref.read(syncProvider).vanStock();
      if (!mounted) return;
      setState(() {
        _van = van;
        _error = null;
      });
    } on OfflineException {
      // Keep whatever is already on screen: a stale load sheet beats a blank one.
      if (mounted) setState(() => _error = 'No connection. Showing the last figures.');
    } on ApiException catch (failure) {
      if (mounted) setState(() => _error = failure.message);
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final lines = (_van?['lines'] as List<dynamic>? ?? const [])
        .whereType<Map<String, dynamic>>()
        .toList();

    return Scaffold(
      appBar: AppBar(
        title: const Text('Van'),
        actions: [
          IconButton(tooltip: 'Refresh', icon: const Icon(Icons.sync), onPressed: _load),
        ],
      ),
      floatingActionButton: FloatingActionButton.extended(
        onPressed: _recordLoad,
        icon: const Icon(Icons.add),
        label: const Text('Took from warehouse'),
      ),
      body: Column(
        children: [
          const SyncBanner(),
          if (_error != null)
            Container(
              width: double.infinity,
              color: Theme.of(context).colorScheme.surfaceContainerHighest,
              padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
              child: Text(_error!, style: TextStyle(color: Theme.of(context).hintColor)),
            ),
          Expanded(
            child: RefreshIndicator(
              onRefresh: _load,
              child: _loading && _van == null
                  ? const Center(child: CircularProgressIndicator())
                  : lines.isEmpty
                      ? const _EmptyVan()
                      : ListView.separated(
                          itemCount: lines.length,
                          separatorBuilder: (_, _) => const Divider(height: 1),
                          itemBuilder: (context, index) => _VanRow(line: lines[index]),
                        ),
            ),
          ),
        ],
      ),
    );
  }

  Future<void> _recordLoad() async {
    final lines = await Navigator.of(context).push<List<SaleLine>>(
      MaterialPageRoute(builder: (_) => const _LoadSheetScreen()),
    );

    if (lines == null || lines.isEmpty || !mounted) return;

    await ref.read(salesRepositoryProvider).recordVanLoad(lines: lines);
    unawaitedSync(ref);

    if (!mounted) return;

    ScaffoldMessenger.of(context).showSnackBar(
      const SnackBar(content: Text('Load recorded. It goes up with the next sync.')),
    );

    await _load();
  }
}

class _VanRow extends StatelessWidget {
  const _VanRow({required this.line});

  final Map<String, dynamic> line;

  double _of(String key) => (line[key] as num?)?.toDouble() ?? 0;

  @override
  Widget build(BuildContext context) {
    final remaining = _of('unaccounted');

    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 12),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Expanded(
                child: Text(line['productName'] as String? ?? '',
                    style: const TextStyle(fontWeight: FontWeight.w500)),
              ),
              Text(
                quantity(remaining),
                style: Theme.of(context)
                    .textTheme
                    .titleLarge
                    ?.copyWith(fontWeight: FontWeight.w600),
              ),
            ],
          ),
          const SizedBox(height: 2),
          Row(
            children: [
              Expanded(
                child: Text(
                  'Took ${quantity(_of('loaded'))} · delivered ${quantity(_of('sold'))}'
                  '${_of('returned') > 0 ? ' · returned ${quantity(_of('returned'))}' : ''}',
                  style: TextStyle(fontSize: 12, color: Theme.of(context).hintColor),
                ),
              ),
              Text('left', style: TextStyle(fontSize: 12, color: Theme.of(context).hintColor)),
            ],
          ),
        ],
      ),
    );
  }
}

class _EmptyVan extends StatelessWidget {
  const _EmptyVan();

  @override
  Widget build(BuildContext context) => ListView(
        children: [
          Padding(
            padding: const EdgeInsets.all(32),
            child: Column(
              children: [
                const Icon(Icons.local_shipping_outlined, size: 40),
                const SizedBox(height: 12),
                Text('Nothing on the van', style: Theme.of(context).textTheme.titleMedium),
                const SizedBox(height: 4),
                const Text(
                  'Record what you took from the warehouse and it will show here.',
                  textAlign: TextAlign.center,
                ),
              ],
            ),
          ),
        ],
      );
}

/// What was taken from the warehouse. Quantities only - there is no price here, because nothing is
/// being sold.
class _LoadSheetScreen extends ConsumerStatefulWidget {
  const _LoadSheetScreen();

  @override
  ConsumerState<_LoadSheetScreen> createState() => _LoadSheetScreenState();
}

class _LoadSheetScreenState extends ConsumerState<_LoadSheetScreen> {
  final Map<String, double> _quantities = {};
  List<CachedProduct> _products = const [];

  @override
  void initState() {
    super.initState();
    ref.read(databaseProvider).allProducts().then((products) {
      if (mounted) setState(() => _products = products);
    });
  }

  List<SaleLine> get _lines => [
        for (final entry in _quantities.entries)
          if (entry.value > 0)
            SaleLine(
              productId: entry.key,
              productName: _products.firstWhere((p) => p.id == entry.key).name,
              quantity: entry.value,
              unitPrice: 0,
            )
      ];

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Took from warehouse')),
      bottomNavigationBar: _lines.isEmpty
          ? null
          : SafeArea(
              child: Padding(
                padding: const EdgeInsets.all(12),
                child: FilledButton(
                  onPressed: () => Navigator.of(context).pop(_lines),
                  child: Text('Record ${_lines.length} product${_lines.length == 1 ? '' : 's'}'),
                ),
              ),
            ),
      body: ListView.separated(
        itemCount: _products.length,
        separatorBuilder: (_, _) => const Divider(height: 1),
        itemBuilder: (context, index) {
          final product = _products[index];
          final value = _quantities[product.id] ?? 0;

          return Padding(
            padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 6),
            child: Row(
              children: [
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(product.name, style: const TextStyle(fontWeight: FontWeight.w500)),
                      Text(product.unitCode.toLowerCase(),
                          style: TextStyle(fontSize: 12, color: Theme.of(context).hintColor)),
                    ],
                  ),
                ),
                SizedBox(
                  width: 96,
                  child: TextField(
                    textAlign: TextAlign.center,
                    keyboardType: const TextInputType.numberWithOptions(decimal: true),
                    inputFormatters: [FilteringTextInputFormatter.allow(RegExp(r'[0-9.]'))],
                    decoration: const InputDecoration(isDense: true, hintText: '0'),
                    onChanged: (text) => setState(() {
                      final parsed = double.tryParse(text) ?? 0;
                      if (parsed <= 0) {
                        _quantities.remove(product.id);
                      } else {
                        _quantities[product.id] = parsed;
                      }
                    }),
                    onTapOutside: (_) => FocusManager.instance.primaryFocus?.unfocus(),
                  ),
                ),
                if (value > 0) const Icon(Icons.check, size: 18),
              ],
            ),
          );
        },
      ),
    );
  }
}
