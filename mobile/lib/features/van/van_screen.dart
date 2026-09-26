import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../app.dart';
import '../../core/money.dart';
import '../../core/theme.dart';
import '../../core/widgets.dart';
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

    double sum(String key) =>
        lines.fold(0, (total, line) => total + ((line[key] as num?)?.toDouble() ?? 0));

    return Scaffold(
      appBar: AppBar(title: const Text('Van Stock'), actions: const [PendingBadge()]),
      // The four tabs stay mounted at once (IndexedStack), so Van's and Orders' FABs need distinct
      // hero tags - otherwise Flutter finds two "default" heroes in the tree and asserts.
      floatingActionButton: FloatingActionButton.extended(
        heroTag: 'van-fab',
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
              color: AppColors.surfaceAlt,
              padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
              child: Text(_error!, style: const TextStyle(color: AppColors.textMuted)),
            ),
          Expanded(
            child: RefreshIndicator(
              onRefresh: _load,
              child: _loading && _van == null
                  ? const Center(child: CircularProgressIndicator())
                  : lines.isEmpty
                      ? const _EmptyVan()
                      : FadeIn(
                          child: ListView(
                            padding: const EdgeInsets.fromLTRB(12, 12, 12, 80),
                            children: [
                              Row(
                                children: [
                                  Expanded(
                                    child: StatTile(
                                        icon: Icons.local_shipping_outlined,
                                        label: 'Loaded',
                                        value: quantity(sum('loaded')),
                                        note: 'units'),
                                  ),
                                  const SizedBox(width: 10),
                                  Expanded(
                                    child: StatTile(
                                        icon: Icons.inventory_2_outlined,
                                        label: 'Sold',
                                        value: quantity(sum('sold')),
                                        note: 'today'),
                                  ),
                                  const SizedBox(width: 10),
                                  Expanded(
                                    child: StatTile(
                                        icon: Icons.local_shipping,
                                        label: 'Remaining',
                                        value: quantity(sum('unaccounted')),
                                        note: 'units'),
                                  ),
                                ],
                              ),
                              const SectionHeader('Products on van'),
                              AppCard(
                                padding: EdgeInsets.zero,
                                child: Column(
                                  children: [
                                    for (final line in lines) ...[
                                      _VanRow(line: line),
                                      if (line != lines.last) const Divider(height: 1),
                                    ],
                                  ],
                                ),
                              ),
                              const SizedBox(height: 10),
                              if (_error == null)
                                Container(
                                  padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 14),
                                  decoration: BoxDecoration(
                                    color: AppColors.successSoft,
                                    borderRadius: BorderRadius.circular(AppRadius.md),
                                  ),
                                  child: const Row(
                                    children: [
                                      Icon(Icons.check_circle_outline, color: AppColors.success),
                                      SizedBox(width: 10),
                                      Text('Stock figures are up to date',
                                          style: TextStyle(
                                              color: AppColors.success, fontWeight: FontWeight.w600)),
                                    ],
                                  ),
                                ),
                            ],
                          ),
                        ),
            ),
          ),
        ],
      ),
    );
  }

  Future<void> _recordLoad() async {
    final lines = await Navigator.of(context).push<List<SaleLine>>(
      appRoute<List<SaleLine>>((_) => const _LoadSheetScreen()),
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
    final loaded = _of('loaded');
    final remaining = _of('unaccounted');
    final progress = loaded <= 0 ? 0.0 : (remaining / loaded).clamp(0, 1).toDouble();

    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 14),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(line['productName'] as String? ?? '',
                        style: const TextStyle(fontWeight: FontWeight.w700)),
                    Text(
                      'Took ${quantity(_of('loaded'))} · delivered ${quantity(_of('sold'))}'
                      '${_of('returned') > 0 ? ' · returned ${quantity(_of('returned'))}' : ''}'
                      '${_of('replaced') > 0 ? ' · replaced free ${quantity(_of('replaced'))}' : ''}',
                      style: const TextStyle(fontSize: 12, color: AppColors.textMuted),
                    ),
                  ],
                ),
              ),
              Text(quantity(remaining),
                  style: const TextStyle(fontSize: 20, fontWeight: FontWeight.w700)),
            ],
          ),
          const SizedBox(height: 8),
          ClipRRect(
            borderRadius: BorderRadius.circular(AppRadius.pill),
            child: LinearProgressIndicator(
              value: progress,
              minHeight: 5,
              backgroundColor: AppColors.surfaceAlt,
              valueColor: const AlwaysStoppedAnimation(AppColors.gold),
            ),
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
        children: const [
          Padding(
            padding: EdgeInsets.only(top: 24),
            child: EmptyState(
              icon: Icons.local_shipping_outlined,
              title: 'Nothing on the van',
              message: 'Record what you took from the warehouse and it will show here.',
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
  final Map<String, TextEditingController> _qtyControllers = {};
  List<CachedProduct> _products = const [];

  @override
  void initState() {
    super.initState();
    ref.read(databaseProvider).allProducts().then((products) {
      if (mounted) setState(() => _products = products);
    });
  }

  @override
  void dispose() {
    for (final controller in _qtyControllers.values) {
      controller.dispose();
    }
    super.dispose();
  }

  TextEditingController _controllerFor(String productId) =>
      _qtyControllers.putIfAbsent(productId, TextEditingController.new);

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
        padding: const EdgeInsets.all(12),
        itemCount: _products.length,
        separatorBuilder: (_, _) => const Divider(height: 1),
        itemBuilder: (context, index) {
          final product = _products[index];

          return ProductQuantityRow(
            name: product.name,
            unit: product.unitCode.toLowerCase(),
            controller: _controllerFor(product.id),
            onChanged: (text) => setState(() {
              final parsed = double.tryParse(text) ?? 0;
              if (parsed <= 0) {
                _quantities.remove(product.id);
              } else {
                _quantities[product.id] = parsed;
              }
            }),
          );
        },
      ),
    );
  }
}
