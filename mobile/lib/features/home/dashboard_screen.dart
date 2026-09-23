import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../app.dart';
import '../../core/money.dart';
import '../../core/theme.dart';
import '../../core/widgets.dart';
import '../../data/local/database.dart';
import '../../data/remote/api_client.dart';
import '../payment/payment_screen.dart';
import '../sale/sale_screen.dart';
import '../sync/sync_views.dart';
import '../van/van_screen.dart';

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
  int _totalShops = 0;
  bool _loaded = false;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    final day = await ref.read(syncProvider).cachedDay();
    final shops = await ref.read(databaseProvider).allCustomers();
    if (!mounted) return;

    setState(() {
      _day = day;
      _totalShops = shops.length;
      _loaded = true;
    });
  }

  Future<void> _refresh() async {
    await ref.read(syncProvider).syncNow();
    await _load();
  }

  /// Home -> the bill page, where the shop is chosen alongside the products. It is the same
  /// screen a shop's page opens, so there is one way a bill is written.
  Future<void> _newBill() async {
    await Navigator.of(context).push(appRoute<void>((_) => const SaleScreen()));

    // A saved bill changes today's figures and the pending count.
    if (mounted) await _load();
  }

  /// Home has no shop of its own, so collecting a payment from here means picking one first -
  /// the same rule the existing payment screen already enforces, just asked one screen earlier.
  Future<void> _receivePayment() async {
    final shops = await ref.read(databaseProvider).allCustomers();
    if (!mounted) return;

    final shop = await showModalBottomSheet<CachedCustomer>(
      context: context,
      isScrollControlled: true,
      builder: (context) => _ShopPickerSheet(shops: shops),
    );

    if (shop == null || !mounted) return;

    await Navigator.of(context).push(appRoute<void>((_) => PaymentScreen(shop: shop)));
    if (mounted) await _load();
  }

  double _money(String key) => (_day?[key] as num?)?.toDouble() ?? 0;

  int _count(String key) => (_day?[key] as num?)?.toInt() ?? 0;

  static String _greeting() {
    final hour = DateTime.now().hour;
    if (hour < 12) return 'Good morning';
    if (hour < 17) return 'Good afternoon';
    return 'Good evening';
  }

  static String _today() {
    const days = ['Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat', 'Sun'];
    const months = [
      'Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun',
      'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec',
    ];
    final now = DateTime.now();

    return '${days[now.weekday - 1]}, ${now.day} ${months[now.month - 1]}';
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      body: SafeArea(
        bottom: false,
        child: Column(
          children: [
            Padding(
              padding: const EdgeInsets.fromLTRB(16, 12, 16, 4),
              child: Row(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text(_today().toUpperCase(), style: kEyebrowStyle),
                        const SizedBox(height: 2),
                        Text(_greeting(), style: Theme.of(context).textTheme.headlineSmall),
                      ],
                    ),
                  ),
                  const PendingBadge(),
                ],
              ),
            ),
            const SyncBanner(),
            Expanded(
              child: RefreshIndicator(
                onRefresh: _refresh,
                child: !_loaded
                    ? const Center(child: CircularProgressIndicator())
                    : FadeIn(
                        child: ListView(
                          padding: const EdgeInsets.fromLTRB(12, 8, 12, 24),
                          children: [
                            _ActionCard(
                              icon: Icons.add,
                              title: 'New Bill',
                              subtitle: 'Record a new shop sale',
                              primary: true,
                              onTap: _newBill,
                            ),
                            const SizedBox(height: 10),
                            _ActionCard(
                              icon: Icons.payments_outlined,
                              title: 'Receive Payment',
                              subtitle: 'Note down a payment from a shop',
                              primary: false,
                              onTap: _receivePayment,
                            ),
                            if (_day == null)
                              const _NothingYet()
                            else ...[
                              const SectionHeader('Today so far'),
                              Row(
                                children: [
                                  Expanded(
                                    child: StatTile(
                                      icon: Icons.currency_rupee,
                                      label: 'Sales today',
                                      value: money(_money('totalSales')),
                                      note: '${_count('saleCount')} delivery',
                                    ),
                                  ),
                                  const SizedBox(width: 10),
                                  Expanded(
                                    child: StatTile(
                                      icon: Icons.credit_card_outlined,
                                      label: 'Collected',
                                      value: money(_money('cashCollected')),
                                      note: 'paid on spot',
                                    ),
                                  ),
                                ],
                              ),
                              const SizedBox(height: 10),
                              Row(
                                children: [
                                  Expanded(
                                    child: StatTile(
                                      icon: Icons.credit_score_outlined,
                                      label: 'On credit',
                                      value: money(_money('creditSales')),
                                      note: 'settled later',
                                    ),
                                  ),
                                  const SizedBox(width: 10),
                                  Expanded(
                                    child: StatTile(
                                      icon: Icons.storefront_outlined,
                                      label: 'Shops visited',
                                      value: _totalShops == 0
                                          ? '${_count('shopsVisited')}'
                                          : '${_count('shopsVisited')} / $_totalShops',
                                      note: "today's route",
                                    ),
                                  ),
                                ],
                              ),
                            ],
                            const TodaysBills(),
                            const _VanSummaryCard(),
                            const SizedBox(height: 10),
                            _PendingTile(pending: ref.watch(pendingCountProvider).value ?? 0),
                          ],
                        ),
                      ),
              ),
            ),
          ],
        ),
      ),
    );
  }
}

/// The two things a salesperson does from Home - one large and gold, one white and secondary but
/// still a full tap target, so the pair reads as related without competing.
class _ActionCard extends StatelessWidget {
  const _ActionCard({
    required this.icon,
    required this.title,
    required this.subtitle,
    required this.primary,
    required this.onTap,
  });

  final IconData icon;
  final String title;
  final String subtitle;
  final bool primary;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return Pressable(
      onTap: onTap,
      child: Container(
        decoration: BoxDecoration(
          color: primary ? AppColors.gold : AppColors.surface,
          borderRadius: BorderRadius.circular(AppRadius.md),
          border: primary ? null : Border.all(color: AppColors.border),
        ),
        padding: const EdgeInsets.all(16),
        child: Row(
          children: [
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    title,
                    style: TextStyle(
                      color: primary ? Colors.white : AppColors.charcoal,
                      fontSize: primary ? 19 : 16,
                      fontWeight: FontWeight.w700,
                    ),
                  ),
                  const SizedBox(height: 2),
                  Text(
                    subtitle,
                    style: TextStyle(
                      color: primary ? const Color(0xFFFBF0DC) : AppColors.textMuted,
                      fontSize: 13,
                    ),
                  ),
                ],
              ),
            ),
            const SizedBox(width: 12),
            Container(
              width: 40,
              height: 40,
              alignment: Alignment.center,
              decoration: BoxDecoration(
                color: primary ? Colors.white.withValues(alpha: 0.2) : AppColors.goldSoft,
                shape: BoxShape.circle,
              ),
              child: Icon(icon, size: 20, color: primary ? Colors.white : AppColors.goldDark),
            ),
          ],
        ),
      ),
    );
  }
}

/// Pick a shop, nothing else - used only to get to the existing payment screen from Home, which
/// has no shop of its own to hand it.
class _ShopPickerSheet extends StatefulWidget {
  const _ShopPickerSheet({required this.shops});

  final List<CachedCustomer> shops;

  @override
  State<_ShopPickerSheet> createState() => _ShopPickerSheetState();
}

class _ShopPickerSheetState extends State<_ShopPickerSheet> {
  final _search = TextEditingController();
  String _term = '';

  @override
  void dispose() {
    _search.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final needle = _term.trim().toLowerCase();
    final shops = needle.isEmpty
        ? widget.shops
        : widget.shops
            .where((s) =>
                s.name.toLowerCase().contains(needle) ||
                (s.phone ?? '').toLowerCase().contains(needle))
            .toList();

    return SafeArea(
      child: Padding(
        padding: EdgeInsets.only(bottom: MediaQuery.of(context).viewInsets.bottom),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Padding(
              padding: const EdgeInsets.fromLTRB(16, 12, 16, 8),
              child: Row(
                children: [
                  Expanded(
                      child: Text('Choose a shop', style: Theme.of(context).textTheme.titleMedium)),
                  IconButton(
                    icon: const Icon(Icons.close),
                    onPressed: () => Navigator.of(context).pop(),
                  ),
                ],
              ),
            ),
            Padding(
              padding: const EdgeInsets.symmetric(horizontal: 16),
              child: TextField(
                controller: _search,
                autofocus: true,
                onChanged: (value) => setState(() => _term = value),
                decoration: const InputDecoration(
                  hintText: 'Find a shop',
                  prefixIcon: Icon(Icons.search),
                ),
              ),
            ),
            const SizedBox(height: 8),
            ConstrainedBox(
              constraints: BoxConstraints(maxHeight: MediaQuery.of(context).size.height * 0.5),
              child: shops.isEmpty
                  ? const Padding(
                      padding: EdgeInsets.all(32),
                      child: Text('No shop by that name', textAlign: TextAlign.center),
                    )
                  : ListView.separated(
                      shrinkWrap: true,
                      itemCount: shops.length,
                      separatorBuilder: (_, _) => const Divider(height: 1),
                      itemBuilder: (context, index) {
                        final shop = shops[index];

                        return ListTile(
                          title: Text(shop.name, style: const TextStyle(fontWeight: FontWeight.w600)),
                          subtitle: shop.balance > 0 ? Text('Owes ${money(shop.balance)}') : null,
                          onTap: () => Navigator.of(context).pop(shop),
                        );
                      },
                    ),
            ),
          ],
        ),
      ),
    );
  }
}

/// The honest caveat on every figure above: this much has not reached the office yet.
class _PendingTile extends StatelessWidget {
  const _PendingTile({required this.pending});

  final int pending;

  @override
  Widget build(BuildContext context) {
    if (pending == 0) {
      return Container(
        padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 14),
        decoration: BoxDecoration(
          color: AppColors.successSoft,
          borderRadius: BorderRadius.circular(AppRadius.md),
        ),
        child: const Row(
          children: [
            Icon(Icons.cloud_done_outlined, color: AppColors.success),
            SizedBox(width: 12),
            Text('All synced', style: TextStyle(fontWeight: FontWeight.w700, color: AppColors.success)),
          ],
        ),
      );
    }

    return InkWell(
      borderRadius: BorderRadius.circular(AppRadius.md),
      onTap: () => Navigator.of(context).push(appRoute<void>((_) => const PendingScreen())),
      child: Container(
        padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 14),
        decoration: BoxDecoration(
          color: AppColors.goldSoft,
          borderRadius: BorderRadius.circular(AppRadius.md),
        ),
        child: Row(
          children: [
            const Icon(Icons.cloud_upload_outlined, color: AppColors.goldDark),
            const SizedBox(width: 12),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text('$pending waiting to sync',
                      style: const TextStyle(fontWeight: FontWeight.w700, color: AppColors.goldDark)),
                  const Text('Saved safely on this phone',
                      style: TextStyle(fontSize: 12.5, color: AppColors.textMuted)),
                ],
              ),
            ),
            const Icon(Icons.chevron_right, color: AppColors.goldDark),
          ],
        ),
      ),
    );
  }
}

/// What is left on the van, at a glance - the same figures as the Van tab. Needs signal like the
/// Van tab does; on a stale phone it simply says so and stays out of the way rather than blocking
/// the rest of the home screen.
class _VanSummaryCard extends ConsumerStatefulWidget {
  const _VanSummaryCard();

  @override
  ConsumerState<_VanSummaryCard> createState() => _VanSummaryCardState();
}

class _VanSummaryCardState extends ConsumerState<_VanSummaryCard> {
  late final Future<Map<String, dynamic>> _future = ref.read(syncProvider).vanStock();

  @override
  Widget build(BuildContext context) {
    return FutureBuilder<Map<String, dynamic>>(
      future: _future,
      builder: (context, snapshot) {
        if (snapshot.connectionState != ConnectionState.done) return const SizedBox.shrink();

        if (snapshot.hasError) {
          final offline = snapshot.error is OfflineException;

          return Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              const SectionHeader('Van stock'),
              AppCard(
                onTap: () => Navigator.of(context).push(appRoute<void>((_) => const VanScreen())),
                child: Row(
                  children: [
                    const Icon(Icons.local_shipping_outlined, color: AppColors.textMuted),
                    const SizedBox(width: 12),
                    Expanded(
                      child: Text(
                        offline
                            ? 'No connection. Open Van for the last figures.'
                            : 'Could not load van stock.',
                        style: const TextStyle(color: AppColors.textMuted),
                      ),
                    ),
                    const Icon(Icons.chevron_right, color: AppColors.textMuted),
                  ],
                ),
              ),
            ],
          );
        }

        final lines = (snapshot.data?['lines'] as List<dynamic>? ?? const [])
            .whereType<Map<String, dynamic>>()
            .toList();

        double sum(String key) =>
            lines.fold(0, (total, line) => total + ((line[key] as num?)?.toDouble() ?? 0));

        final loaded = sum('loaded');
        final sold = sum('sold');
        final remaining = sum('unaccounted');
        final progress = loaded <= 0 ? 0.0 : (sold / loaded).clamp(0, 1).toDouble();

        return Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            const SectionHeader('Van stock'),
            AppCard(
              onTap: () => Navigator.of(context).push(appRoute<void>((_) => const VanScreen())),
              child: Column(
                children: [
                  Row(
                    children: [
                      Expanded(child: _VanFigure(label: 'Loaded', value: quantity(loaded))),
                      Expanded(child: _VanFigure(label: 'Sold', value: quantity(sold))),
                      Expanded(child: _VanFigure(label: 'Remaining', value: quantity(remaining))),
                    ],
                  ),
                  const SizedBox(height: 14),
                  ClipRRect(
                    borderRadius: BorderRadius.circular(AppRadius.pill),
                    child: LinearProgressIndicator(
                      value: progress,
                      minHeight: 6,
                      backgroundColor: AppColors.surfaceAlt,
                      valueColor: const AlwaysStoppedAnimation(AppColors.gold),
                    ),
                  ),
                ],
              ),
            ),
          ],
        );
      },
    );
  }
}

class _VanFigure extends StatelessWidget {
  const _VanFigure({required this.label, required this.value});

  final String label;
  final String value;

  @override
  Widget build(BuildContext context) => Column(
        children: [
          Text(value,
              style: const TextStyle(fontSize: 20, fontWeight: FontWeight.w700, color: AppColors.charcoal)),
          const SizedBox(height: 2),
          Text(label.toUpperCase(), style: kEyebrowStyle),
        ],
      );
}

class _NothingYet extends StatelessWidget {
  const _NothingYet();

  @override
  Widget build(BuildContext context) => const Padding(
        padding: EdgeInsets.only(top: 24),
        child: EmptyState(
          icon: Icons.today_outlined,
          title: 'No summary yet',
          message: 'Sync once with a connection and the day so far will be here.',
        ),
      );
}

/// Every bill written on this phone today. Until the office has it a bill has no number - the phone
/// never invents one - so it reads "waiting to sync"; once synced it shows the official number the
/// server gave it, the one printed on the invoice.
class TodaysBills extends ConsumerWidget {
  const TodaysBills({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final now = DateTime.now();
    final startOfDay = DateTime(now.year, now.month, now.day);

    return StreamBuilder<List<OutboxEntry>>(
      stream: ref.watch(databaseProvider).watchOutboxOfType('Invoice'),
      builder: (context, snapshot) {
        final today = (snapshot.data ?? const <OutboxEntry>[])
            .where((entry) => !entry.recordedAt.toLocal().isBefore(startOfDay))
            .toList();

        if (today.isEmpty) return const SizedBox.shrink();

        return Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            const SectionHeader("Today's bills"),
            AppCard(
              padding: EdgeInsets.zero,
              child: Column(
                children: [
                  for (final entry in today) _BillRow(entry: entry),
                ],
              ),
            ),
          ],
        );
      },
    );
  }
}

class _BillRow extends StatelessWidget {
  const _BillRow({required this.entry});

  final OutboxEntry entry;

  @override
  Widget build(BuildContext context) {
    final time = timeOfDay(entry.recordedAt);
    final (icon, colour, detail) = switch (entry.status) {
      'Synced' => (
          Icons.check_circle_outline,
          AppColors.success,
          entry.documentNumber == null ? 'With the office · $time' : '${entry.documentNumber} · $time',
        ),
      'Failed' => (Icons.error_outline, AppColors.danger, entry.lastError ?? 'The office refused this.'),
      _ => (Icons.schedule, AppColors.textMuted, 'Waiting to sync · $time'),
    };

    return ListTile(
      dense: true,
      leading: Icon(icon, color: colour),
      title: Text(entry.summary, style: const TextStyle(fontWeight: FontWeight.w600)),
      subtitle: Text(detail),
    );
  }
}
