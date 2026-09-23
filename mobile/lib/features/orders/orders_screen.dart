import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../app.dart';
import '../../core/money.dart';
import '../../core/theme.dart';
import '../../core/widgets.dart';
import '../../data/local/database.dart';
import '../../data/sales_repository.dart';
import '../sync/sync_views.dart';

/// What the salesperson needs the packing unit to pack, and when.
///
/// Not a customer order: nothing is billed, no stock moves and nothing is reserved. It only
/// replaces walking into the packing room and telling somebody what tomorrow looks like. One
/// product at a time, because that is how the request is actually said out loud.
class OrdersScreen extends ConsumerStatefulWidget {
  const OrdersScreen({super.key});

  @override
  ConsumerState<OrdersScreen> createState() => _OrdersScreenState();
}

class _OrdersScreenState extends ConsumerState<OrdersScreen> {
  List<CachedProduct> _products = const [];
  String? _productId;
  final _quantity = TextEditingController();
  DateTime _requiredDate = DateTime.now().add(const Duration(days: 1));
  final _notes = TextEditingController();
  bool _notesOpen = false;
  bool _saving = false;

  @override
  void initState() {
    super.initState();
    ref.read(databaseProvider).allProducts().then((products) {
      if (mounted) setState(() => _products = products);
    });
  }

  @override
  void dispose() {
    _quantity.dispose();
    _notes.dispose();
    super.dispose();
  }

  double get _quantityValue => double.tryParse(_quantity.text) ?? 0;

  bool get _canSubmit => _productId != null && _quantityValue > 0 && !_saving;

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

  Future<void> _submit() async {
    final product = _products.firstWhere((p) => p.id == _productId);

    setState(() => _saving = true);

    try {
      await ref.read(salesRepositoryProvider).recordStockRequest(
        requiredDate: _requiredDate,
        lines: [
          SaleLine(
            productId: product.id,
            productName: product.name,
            quantity: _quantityValue,
            unitPrice: 0,
          ),
        ],
        notes: _notes.text.trim().isEmpty ? null : _notes.text.trim(),
      );

      unawaitedSync(ref);

      if (!mounted) return;

      setState(() {
        _productId = null;
        _quantity.clear();
        _notes.clear();
        _notesOpen = false;
      });

      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Request saved. It goes up with the next sync.')),
      );
    } finally {
      if (mounted) setState(() => _saving = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final db = ref.watch(databaseProvider);

    return Scaffold(
      appBar: AppBar(title: const Text('Orders'), actions: const [PendingBadge()]),
      body: RefreshIndicator(
        onRefresh: () => ref.read(syncProvider).syncNow(),
        child: ListView(
          padding: const EdgeInsets.fromLTRB(12, 8, 12, 24),
          children: [
            const SyncBanner(),
            const SizedBox(height: 8),
            StreamBuilder<List<OutboxEntry>>(
              stream: db.watchOutboxOfType('StockRequest'),
              builder: (context, snapshot) => _RequestStats(entries: snapshot.data ?? const []),
            ),
            const SectionHeader('New stock request'),
            AppCard(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  DropdownButtonFormField<String>(
                    key: const ValueKey('request-product'),
                    initialValue: _productId,
                    isExpanded: true,
                    decoration: const InputDecoration(labelText: 'Product'),
                    icon: const Icon(Icons.expand_more),
                    items: [
                      for (final product in _products)
                        DropdownMenuItem(
                          value: product.id,
                          child: Text(product.name, overflow: TextOverflow.ellipsis),
                        ),
                    ],
                    onChanged: (value) => setState(() => _productId = value),
                  ),
                  const SizedBox(height: 16),
                  Row(
                    children: [
                      const Expanded(
                        child: Text('Quantity', style: TextStyle(fontWeight: FontWeight.w600)),
                      ),
                      QuantityStepper(
                        controller: _quantity,
                        fieldKey: const ValueKey('request-quantity'),
                        onChanged: (_) => setState(() {}),
                      ),
                    ],
                  ),
                  const SizedBox(height: 16),
                  InkWell(
                    borderRadius: BorderRadius.circular(AppRadius.md),
                    onTap: _pickDate,
                    child: InputDecorator(
                      decoration: const InputDecoration(labelText: 'Required date'),
                      child: Row(
                        children: [
                          Expanded(child: Text(_formatDate(_requiredDate))),
                          const Icon(Icons.calendar_today_outlined, size: 18),
                        ],
                      ),
                    ),
                  ),
                  if (!_notesOpen)
                    Align(
                      alignment: Alignment.centerLeft,
                      child: TextButton(
                        onPressed: () => setState(() => _notesOpen = true),
                        child: const Text('Add a note'),
                      ),
                    )
                  else ...[
                    const SizedBox(height: 12),
                    TextField(
                      controller: _notes,
                      decoration: const InputDecoration(labelText: 'Notes', hintText: 'Optional'),
                    ),
                  ],
                  const SizedBox(height: 16),
                  FilledButton(
                    onPressed: _canSubmit ? _submit : null,
                    child: _saving
                        ? const SizedBox(
                            height: 20,
                            width: 20,
                            child: CircularProgressIndicator(strokeWidth: 2, color: Colors.white))
                        : const Text('Submit Request'),
                  ),
                ],
              ),
            ),
            const SectionHeader('Recent requests'),
            StreamBuilder<List<OutboxEntry>>(
              stream: db.watchOutboxOfType('StockRequest'),
              builder: (context, snapshot) {
                final requests = snapshot.data ?? const <OutboxEntry>[];

                if (requests.isEmpty) {
                  return const Padding(
                    padding: EdgeInsets.symmetric(vertical: 8),
                    child: EmptyState(
                      icon: Icons.inventory_2_outlined,
                      title: 'Nothing asked for yet',
                      message: 'Tell the packing unit what you need and when. It works with no signal.',
                    ),
                  );
                }

                return AppCard(
                  padding: EdgeInsets.zero,
                  child: Column(
                    children: [
                      for (final request in requests) ...[
                        _RequestTile(request: request, onRetry: () => db.retryNow(request.clientRequestId)),
                        if (request != requests.last) const Divider(height: 1),
                      ],
                    ],
                  ),
                );
              },
            ),
          ],
        ),
      ),
    );
  }

  static String _formatDate(DateTime date) =>
      '${date.day.toString().padLeft(2, '0')}-${date.month.toString().padLeft(2, '0')}-${date.year}';
}

/// Pending / Submitted / Synced / Failed, straight from the outbox - "submitted" is the existing
/// Syncing status in words a salesperson uses, not a new state.
class _RequestStats extends StatelessWidget {
  const _RequestStats({required this.entries});

  final List<OutboxEntry> entries;

  int _count(String status) => entries.where((e) => e.status == status).length;

  @override
  Widget build(BuildContext context) {
    final stats = [
      ('Pending', _count('Pending'), AppColors.textMuted),
      ('Submitted', _count('Syncing'), AppColors.textMuted),
      ('Synced', _count('Synced'), AppColors.success),
      ('Failed', _count('Failed'), AppColors.danger),
    ];

    return AppCard(
      child: Row(
        children: [
          for (final (label, count, color) in stats)
            Expanded(
              child: Column(
                children: [
                  Text('$count',
                      style: TextStyle(fontSize: 20, fontWeight: FontWeight.w700, color: color)),
                  const SizedBox(height: 2),
                  Text(label.toUpperCase(), style: kEyebrowStyle),
                ],
              ),
            ),
        ],
      ),
    );
  }
}

class _RequestTile extends StatelessWidget {
  const _RequestTile({required this.request, required this.onRetry});

  final OutboxEntry request;
  final VoidCallback onRetry;

  @override
  Widget build(BuildContext context) {
    final failed = request.status == 'Failed';
    final waiting = request.status == 'Pending' || request.status == 'Syncing';

    return ListTile(
      leading: Icon(
        failed
            ? Icons.error_outline
            : waiting
                ? Icons.schedule
                : Icons.check_circle_outline,
        color: failed
            ? AppColors.danger
            : waiting
                ? AppColors.warning
                : AppColors.success,
      ),
      title: Text(request.summary, style: const TextStyle(fontWeight: FontWeight.w600)),
      subtitle: Text(
        failed
            ? request.lastError ?? 'The office refused this.'
            : waiting
                ? 'Saved here. It will go up on its own.'
                : 'The packing unit has it · ${dayAndTime(request.recordedAt)}',
      ),
      trailing: failed ? TextButton(onPressed: onRetry, child: const Text('Try again')) : null,
    );
  }
}
