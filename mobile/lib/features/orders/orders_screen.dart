import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../app.dart';
import '../../core/money.dart';
import '../../data/local/database.dart';
import '../../data/sales_repository.dart';
import '../sync/sync_views.dart';

/// What the salesperson needs the packing unit to pack, and when.
///
/// Not a customer order: nothing is billed, no stock moves and nothing is reserved. It only
/// replaces walking into the packing room and telling somebody what tomorrow looks like.
class OrdersScreen extends ConsumerWidget {
  const OrdersScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final db = ref.watch(databaseProvider);

    return Scaffold(
      appBar: AppBar(title: const Text('Ask for stock')),
      floatingActionButton: FloatingActionButton.extended(
        onPressed: () => _compose(context, ref),
        icon: const Icon(Icons.add),
        label: const Text('New request'),
      ),
      body: Column(
        children: [
          const SyncBanner(),
          Expanded(
            child: StreamBuilder<List<OutboxEntry>>(
              // Requests are shown from the outbox, which means one appears the instant it is
              // written and stays visible whether or not the office has it yet.
              stream: db.watchOutboxOfType('StockRequest'),
              builder: (context, snapshot) {
                final requests = snapshot.data ?? const <OutboxEntry>[];

                if (requests.isEmpty) return const _NoRequests();

                return ListView.separated(
                  itemCount: requests.length,
                  separatorBuilder: (_, _) => const Divider(height: 1),
                  itemBuilder: (context, index) {
                    final request = requests[index];
                    final failed = request.status == 'Failed';
                    final waiting = request.status == 'Pending' || request.status == 'Syncing';

                    return ListTile(
                      leading: Icon(
                        failed
                            ? Icons.error_outline
                            : waiting
                                ? Icons.schedule
                                : Icons.check_circle_outline,
                        color: failed ? Theme.of(context).colorScheme.error : null,
                      ),
                      title: Text(request.summary),
                      subtitle: Text(
                        failed
                            ? request.lastError ?? 'The office refused this.'
                            : waiting
                                ? 'Saved here. It will go up on its own.'
                                : 'The packing unit has it · ${dayAndTime(request.recordedAt)}',
                      ),
                      trailing: failed
                          ? TextButton(
                              onPressed: () => db.retryNow(request.clientRequestId),
                              child: const Text('Try again'),
                            )
                          : null,
                    );
                  },
                );
              },
            ),
          ),
        ],
      ),
    );
  }

  Future<void> _compose(BuildContext context, WidgetRef ref) async {
    final result = await Navigator.of(context).push<_Request>(
      MaterialPageRoute(builder: (_) => const _NewRequestScreen()),
    );

    if (result == null || !context.mounted) return;

    await ref.read(salesRepositoryProvider).recordStockRequest(
          requiredDate: result.requiredDate,
          lines: result.lines,
          notes: result.notes,
        );

    unawaitedSync(ref);

    if (!context.mounted) return;

    ScaffoldMessenger.of(context).showSnackBar(
      const SnackBar(content: Text('Request saved. It goes up with the next sync.')),
    );
  }
}

class _Request {
  const _Request({required this.requiredDate, required this.lines, this.notes});

  final DateTime requiredDate;
  final List<SaleLine> lines;
  final String? notes;
}

class _NewRequestScreen extends ConsumerStatefulWidget {
  const _NewRequestScreen();

  @override
  ConsumerState<_NewRequestScreen> createState() => _NewRequestScreenState();
}

class _NewRequestScreenState extends ConsumerState<_NewRequestScreen> {
  final Map<String, double> _quantities = {};
  final _notes = TextEditingController();

  // Tomorrow, because that is what a request nearly always means.
  DateTime _requiredDate = DateTime.now().add(const Duration(days: 1));
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
    _notes.dispose();
    super.dispose();
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
      appBar: AppBar(title: const Text('New request')),
      bottomNavigationBar: _lines.isEmpty
          ? null
          : SafeArea(
              child: Padding(
                padding: const EdgeInsets.all(12),
                child: FilledButton(
                  onPressed: () => Navigator.of(context).pop(_Request(
                    requiredDate: _requiredDate,
                    lines: _lines,
                    notes: _notes.text.trim().isEmpty ? null : _notes.text.trim(),
                  )),
                  child: Text('Ask for ${_lines.length} product${_lines.length == 1 ? '' : 's'}'),
                ),
              ),
            ),
      body: ListView(
        children: [
          ListTile(
            leading: const Icon(Icons.event),
            title: const Text('Needed by'),
            subtitle: Text(dayAndTime(_requiredDate).split(',').first),
            trailing: const Icon(Icons.edit_calendar_outlined),
            onTap: _pickDate,
          ),
          const Divider(height: 1),
          for (final product in _products) ...[
            Padding(
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
                ],
              ),
            ),
            const Divider(height: 1),
          ],
          Padding(
            padding: const EdgeInsets.all(12),
            child: TextField(
              controller: _notes,
              decoration: const InputDecoration(labelText: 'Notes', hintText: 'Optional'),
            ),
          ),
          const SizedBox(height: 80),
        ],
      ),
    );
  }

  Future<void> _pickDate() async {
    final today = DateTime.now();
    final picked = await showDatePicker(
      context: context,
      initialDate: _requiredDate,
      firstDate: today,
      lastDate: today.add(const Duration(days: 60)),
    );

    if (picked != null && mounted) setState(() => _requiredDate = picked);
  }
}

class _NoRequests extends StatelessWidget {
  const _NoRequests();

  @override
  Widget build(BuildContext context) => Center(
        child: Padding(
          padding: const EdgeInsets.all(32),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              const Icon(Icons.inventory_2_outlined, size: 40),
              const SizedBox(height: 12),
              Text('Nothing asked for yet', style: Theme.of(context).textTheme.titleMedium),
              const SizedBox(height: 4),
              const Text(
                'Tell the packing unit what you need and when. It works with no signal.',
                textAlign: TextAlign.center,
              ),
            ],
          ),
        ),
      );
}
