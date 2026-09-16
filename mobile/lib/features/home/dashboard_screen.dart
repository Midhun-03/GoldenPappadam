import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../app.dart';
import '../../core/money.dart';
import '../sync/sync_views.dart';

/// The salesperson's own day, at a glance.
///
/// The figures come from the office, cached at the last sync, with the pending count beside them
/// saying how much of today the office has not been told about yet. Showing a confident total that
/// silently excludes four unsent sales would be worse than showing the gap.
class DashboardScreen extends ConsumerStatefulWidget {
  const DashboardScreen({super.key});

  @override
  ConsumerState<DashboardScreen> createState() => _DashboardScreenState();
}

class _DashboardScreenState extends ConsumerState<DashboardScreen> {
  Map<String, dynamic>? _day;
  bool _loaded = false;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    final day = await ref.read(syncProvider).cachedDay();
    if (!mounted) return;

    setState(() {
      _day = day;
      _loaded = true;
    });
  }

  Future<void> _refresh() async {
    await ref.read(syncProvider).syncNow();
    await _load();
  }

  double _money(String key) => (_day?[key] as num?)?.toDouble() ?? 0;

  int _count(String key) => (_day?[key] as num?)?.toInt() ?? 0;

  @override
  Widget build(BuildContext context) {
    final pending = ref.watch(pendingCountProvider).value ?? 0;

    return Scaffold(
      appBar: AppBar(
        title: const Text('Today'),
        actions: [
          IconButton(tooltip: 'Sync now', icon: const Icon(Icons.sync), onPressed: _refresh),
        ],
      ),
      body: Column(
        children: [
          const SyncBanner(),
          Expanded(
            child: RefreshIndicator(
              onRefresh: _refresh,
              child: !_loaded
                  ? const Center(child: CircularProgressIndicator())
                  : ListView(
                      padding: const EdgeInsets.all(12),
                      children: [
                        if (_day == null)
                          const _NothingYet()
                        else ...[
                          _Tile(
                            label: 'Sales today',
                            value: money(_money('totalSales')),
                            note: '${_count('saleCount')} deliveries',
                            emphasis: true,
                          ),
                          Row(
                            children: [
                              Expanded(
                                child: _Tile(
                                  label: 'Collected',
                                  value: money(_money('cashCollected')),
                                  note: 'paid on the spot',
                                ),
                              ),
                              const SizedBox(width: 8),
                              Expanded(
                                child: _Tile(
                                  label: 'On credit',
                                  value: money(_money('creditSales')),
                                  note: 'settled later',
                                ),
                              ),
                            ],
                          ),
                          Row(
                            children: [
                              Expanded(
                                child: _Tile(
                                  label: 'Shops visited',
                                  value: '${_count('shopsVisited')}',
                                  note: 'stops today',
                                ),
                              ),
                              const SizedBox(width: 8),
                              Expanded(
                                child: _Tile(
                                  label: 'Took nothing',
                                  value: '${_count('noSaleVisits')}',
                                  note: 'no order',
                                ),
                              ),
                            ],
                          ),
                          _Tile(
                            label: 'Outstanding, all shops',
                            value: money(_money('totalOutstanding')),
                            note: 'as of ${howLongAgo(_lastSync)}',
                          ),
                        ],
                        _PendingTile(pending: pending),
                      ],
                    ),
            ),
          ),
        ],
      ),
    );
  }

  DateTime? _lastSync;

  @override
  void didChangeDependencies() {
    super.didChangeDependencies();
    ref.read(syncProvider).lastSyncedAt.then((value) {
      if (mounted) setState(() => _lastSync = value);
    });
  }
}

class _Tile extends StatelessWidget {
  const _Tile({
    required this.label,
    required this.value,
    required this.note,
    this.emphasis = false,
  });

  final String label;
  final String value;
  final String note;
  final bool emphasis;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;

    return Card(
      margin: const EdgeInsets.only(bottom: 8),
      color: emphasis ? scheme.primaryContainer : null,
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(label,
                style: TextStyle(
                    color: emphasis ? scheme.onPrimaryContainer : Theme.of(context).hintColor)),
            const SizedBox(height: 4),
            Text(
              value,
              style: Theme.of(context).textTheme.headlineSmall?.copyWith(
                    fontWeight: FontWeight.w600,
                    color: emphasis ? scheme.onPrimaryContainer : null,
                  ),
            ),
            Text(note,
                style: TextStyle(
                    fontSize: 12,
                    color: emphasis ? scheme.onPrimaryContainer : Theme.of(context).hintColor)),
          ],
        ),
      ),
    );
  }
}

/// The honest caveat on every figure above: this much has not reached the office yet.
class _PendingTile extends ConsumerWidget {
  const _PendingTile({required this.pending});

  final int pending;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    if (pending == 0) {
      return const Card(
        margin: EdgeInsets.only(bottom: 8),
        child: ListTile(
          leading: Icon(Icons.cloud_done_outlined),
          title: Text('Everything is with the office'),
          subtitle: Text('The figures above are complete.'),
        ),
      );
    }

    return Card(
      margin: const EdgeInsets.only(bottom: 8),
      color: Theme.of(context).colorScheme.secondaryContainer,
      child: ListTile(
        leading: const Icon(Icons.cloud_upload_outlined),
        title: Text('$pending waiting to go up'),
        subtitle: const Text('Not counted above until the office has it.'),
        trailing: const Icon(Icons.chevron_right),
        onTap: () => Navigator.of(context).push(
          MaterialPageRoute<void>(builder: (_) => const PendingScreen()),
        ),
      ),
    );
  }
}

class _NothingYet extends StatelessWidget {
  const _NothingYet();

  @override
  Widget build(BuildContext context) => Padding(
        padding: const EdgeInsets.all(32),
        child: Column(
          children: [
            const Icon(Icons.today_outlined, size: 40),
            const SizedBox(height: 12),
            Text('No summary yet', style: Theme.of(context).textTheme.titleMedium),
            const SizedBox(height: 4),
            const Text(
              'Sync once with a connection and the day so far will be here.',
              textAlign: TextAlign.center,
            ),
          ],
        ),
      );
}
