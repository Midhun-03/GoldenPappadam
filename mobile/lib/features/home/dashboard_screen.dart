import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../app.dart';
import '../../core/money.dart';
import '../../core/theme.dart';
import '../../core/widgets.dart';
import '../../data/local/database.dart';
import '../../data/remote/api_client.dart';
import '../../sync/sync_engine.dart';
import '../payment/payment_screen.dart';
import '../sale/sale_screen.dart';
import '../sync/sync_views.dart';
import '../van/van_screen.dart';

/// A stronger ease-out than the built-in one: it moves at once and settles gently, which is what
/// makes an entrance feel like a response rather than a delay.
const _easeOut = Cubic(0.23, 1, 0.32, 1);

/// The salesperson's own day, at a glance.
///
/// The figures come from the office, cached at the last sync, with the pending count beside them
/// saying how much of today the office has not been told about yet. Showing a confident total that
/// silently excludes four unsent sales would be worse than showing the gap.
///
/// Reading order, top to bottom: is my work with the office (the pill), how the day stands (the
/// sales card), what I do next (the two actions), then the supporting detail - shops, van, bills.
class DashboardScreen extends ConsumerStatefulWidget {
  const DashboardScreen({super.key});

  @override
  ConsumerState<DashboardScreen> createState() => _DashboardScreenState();
}

class _DashboardScreenState extends ConsumerState<DashboardScreen> {
  Map<String, dynamic>? _day;
  DateTime? _syncedAt;
  int _totalShops = 0;
  bool _loaded = false;

  /// Bumped by pull-to-refresh so the van card asks the office again rather than keeping the
  /// answer it got when the app opened.
  int _refreshTick = 0;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    final sync = ref.read(syncProvider);
    final day = await sync.cachedDay();
    final syncedAt = await sync.lastSyncedAt;
    final shops = await ref.read(databaseProvider).allCustomers();
    if (!mounted) return;

    setState(() {
      _day = day;
      _syncedAt = syncedAt;
      _totalShops = shops.length;
      _loaded = true;
    });
  }

  Future<void> _refresh() async {
    await ref.read(syncProvider).syncNow();
    await _load();
    if (mounted) setState(() => _refreshTick++);
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
    final pending = ref.watch(pendingCountProvider).value ?? 0;
    final status = ref.watch(syncStatusProvider).value;
    final failed = status?.failed ?? 0;

    // Keyed, so a notice arriving at the top does not make the cards under it start again.
    final sections = <(String, Widget)>[
      if (failed > 0) ('failed', _FailedNotice(count: failed)),
      (
        'today',
        _day == null
            ? const _NothingYet()
            : _TodayCard(
                sales: _money('totalSales'),
                saleCount: _count('saleCount'),
                collected: _money('cashCollected'),
                onCredit: _money('creditSales'),
                syncedAt: status?.lastSyncedAt ?? _syncedAt,
                pending: pending,
              ),
      ),
      ('actions', _QuickActions(onNewBill: _newBill, onReceivePayment: _receivePayment)),
      if (_day != null)
        ('shops', _ShopsVisitedCard(visited: _count('shopsVisited'), total: _totalShops)),
      ('van', _VanSummaryCard(key: ValueKey(_refreshTick))),
      ('bills', const TodaysBills()),
    ];

    return Scaffold(
      body: SafeArea(
        bottom: false,
        child: Column(
          children: [
            Padding(
              padding: const EdgeInsets.fromLTRB(20, 12, 16, 8),
              child: Row(
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
                  const SizedBox(width: 12),
                  _SyncPill(pending: pending, status: status),
                ],
              ),
            ),
            Expanded(
              child: RefreshIndicator(
                onRefresh: _refresh,
                child: !_loaded
                    ? const SizedBox.expand()
                    : SingleChildScrollView(
                        physics: const AlwaysScrollableScrollPhysics(),
                        padding: const EdgeInsets.fromLTRB(16, 4, 16, 28),
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.stretch,
                          children: [
                            for (final (index, (id, section)) in sections.indexed)
                              _Enter(key: ValueKey(id), index: index, child: section),
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

/// One section arriving: a fade and a short rise, each a beat after the one above it. It plays
/// once, when Home first has something to show - the tabs stay mounted, so coming back to Home
/// does not replay it. With "remove animations" switched on in Android it only fades.
class _Enter extends StatelessWidget {
  const _Enter({required this.index, required this.child, super.key});

  final int index;
  final Widget child;

  static const _each = 240;
  static const _stagger = 45;

  @override
  Widget build(BuildContext context) {
    final still = MediaQuery.disableAnimationsOf(context);
    final delay = _stagger * index;
    final total = _each + delay;

    return TweenAnimationBuilder<double>(
      tween: Tween(begin: 0, end: 1),
      duration: Duration(milliseconds: total),
      curve: Interval(delay / total, 1, curve: _easeOut),
      builder: (context, t, child) => Opacity(
        opacity: t,
        child: still ? child : Transform.translate(offset: Offset(0, (1 - t) * 10), child: child),
      ),
      child: child,
    );
  }
}

/// Whether the office has today's work, in one word, always in the same corner. It replaces the
/// full-width banner on Home only: here the sales card below explains the gap in full, so the top
/// of the screen can stay quiet when nothing is wrong.
class _SyncPill extends StatelessWidget {
  const _SyncPill({required this.pending, required this.status});

  final int pending;
  final SyncStatus? status;

  @override
  Widget build(BuildContext context) {
    final status = this.status;

    final (Color background, Color foreground, IconData icon, String label) = switch (status) {
      SyncStatus(failed: > 0) => (
          AppColors.dangerSoft,
          AppColors.danger,
          Icons.error_outline,
          '${status.failed} not sent'
        ),
      _ when pending > 0 => (
          AppColors.warningSoft,
          AppColors.warning,
          Icons.cloud_upload_outlined,
          '$pending pending'
        ),
      SyncStatus(state: SyncState.offline) => (
          AppColors.surfaceAlt,
          AppColors.textMuted,
          Icons.cloud_off_outlined,
          'Offline'
        ),
      // Mid-sync with nothing known to be waiting: "Synced" would be a claim made too early.
      SyncStatus(state: SyncState.syncing) => (
          AppColors.surfaceAlt,
          AppColors.textMuted,
          Icons.sync,
          'Syncing'
        ),
      _ => (AppColors.successSoft, AppColors.success, Icons.cloud_done_outlined, 'Synced'),
    };

    return Semantics(
      button: true,
      label: 'Sync status: $label',
      excludeSemantics: true,
      child: Pressable(
        onTap: () => Navigator.of(context).push(appRoute<void>((_) => const PendingScreen())),
        // The pill is small; the area that answers a thumb is not.
        child: Container(
          constraints: const BoxConstraints(minHeight: 48),
          color: Colors.transparent,
          alignment: Alignment.center,
          child: AnimatedContainer(
            duration: AppMotion.normal,
            curve: Curves.ease,
            padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 7),
            decoration: BoxDecoration(
              color: background,
              borderRadius: BorderRadius.circular(AppRadius.pill),
            ),
            child: Row(
              mainAxisSize: MainAxisSize.min,
              children: [
                if (status?.state == SyncState.syncing)
                  SizedBox(
                    width: 14,
                    height: 14,
                    child: CircularProgressIndicator(strokeWidth: 2, color: foreground),
                  )
                else
                  Icon(icon, size: 16, color: foreground),
                const SizedBox(width: 6),
                Text(
                  label,
                  style: TextStyle(color: foreground, fontSize: 13, fontWeight: FontWeight.w700),
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}

/// The one thing on Home that needs a person: the office refused something. Shown only then.
class _FailedNotice extends StatelessWidget {
  const _FailedNotice({required this.count});

  final int count;

  @override
  Widget build(BuildContext context) => Padding(
        padding: const EdgeInsets.only(bottom: 12),
        child: Semantics(
          button: true,
          child: Pressable(
            onTap: () => Navigator.of(context).push(appRoute<void>((_) => const PendingScreen())),
            child: Container(
              constraints: const BoxConstraints(minHeight: 56),
              padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 12),
              decoration: BoxDecoration(
                color: AppColors.dangerSoft,
                borderRadius: BorderRadius.circular(AppRadius.md),
              ),
              child: Row(
                children: [
                  const Icon(Icons.error_outline, color: AppColors.danger),
                  const SizedBox(width: 12),
                  Expanded(
                    child: Text(
                      '$count could not be sent. Tap to see.',
                      style: const TextStyle(color: AppColors.danger, fontWeight: FontWeight.w700),
                    ),
                  ),
                  const Icon(Icons.chevron_right, color: AppColors.danger),
                ],
              ),
            ),
          ),
        ),
      );
}

/// The day in one card: what was sold, large enough to read at arm's length in daylight, then how
/// much of it is money in hand and how much is still owed. The foot of the card says how old the
/// figures are and how much of today they do not include yet.
class _TodayCard extends StatelessWidget {
  const _TodayCard({
    required this.sales,
    required this.saleCount,
    required this.collected,
    required this.onCredit,
    required this.syncedAt,
    required this.pending,
  });

  final double sales;
  final int saleCount;
  final double collected;
  final double onCredit;
  final DateTime? syncedAt;
  final int pending;

  @override
  Widget build(BuildContext context) {
    return AppCard(
      padding: EdgeInsets.zero,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Padding(
            padding: const EdgeInsets.fromLTRB(20, 18, 20, 18),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Row(
                  children: [
                    Expanded(child: Text('SALES TODAY', style: kEyebrowStyle)),
                    StatusPill(saleCount == 1 ? '1 delivery' : '$saleCount deliveries'),
                  ],
                ),
                const SizedBox(height: 6),
                // A long amount shrinks to fit rather than being cut off: a total with its last
                // digits missing is worse than a slightly smaller one.
                FittedBox(
                  fit: BoxFit.scaleDown,
                  alignment: Alignment.centerLeft,
                  child: Text(
                    money(sales),
                    style: const TextStyle(
                      fontSize: 36,
                      height: 1.15,
                      fontWeight: FontWeight.w700,
                      letterSpacing: -0.8,
                      color: AppColors.charcoal,
                      fontFeatures: kTabularFigures,
                    ),
                  ),
                ),
                const SizedBox(height: 18),
                Row(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Expanded(
                      child: _SplitFigure(
                        dot: AppColors.success,
                        label: 'Collected',
                        value: money(collected),
                      ),
                    ),
                    const SizedBox(width: 16),
                    Expanded(
                      child: _SplitFigure(
                        dot: AppColors.gold,
                        label: 'On credit',
                        value: money(onCredit),
                      ),
                    ),
                  ],
                ),
              ],
            ),
          ),
          const Divider(),
          if (pending > 0)
            _PendingFoot(pending: pending)
          else
            Padding(
              padding: const EdgeInsets.symmetric(horizontal: 20, vertical: 13),
              child: Row(
                children: [
                  const Icon(Icons.cloud_done_outlined, size: 16, color: AppColors.textMuted),
                  const SizedBox(width: 8),
                  Expanded(
                    child: Text(
                      syncedAt == null
                          ? 'Office figures'
                          : 'Office figures · updated ${howLongAgo(syncedAt)}',
                      style: const TextStyle(fontSize: 13, color: AppColors.textMuted),
                    ),
                  ),
                ],
              ),
            ),
        ],
      ),
    );
  }
}

class _SplitFigure extends StatelessWidget {
  const _SplitFigure({required this.dot, required this.label, required this.value});

  final Color dot;
  final String label;
  final String value;

  @override
  Widget build(BuildContext context) => Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Container(
                width: 8,
                height: 8,
                decoration: BoxDecoration(color: dot, shape: BoxShape.circle),
              ),
              const SizedBox(width: 6),
              Text(label, style: const TextStyle(fontSize: 13, color: AppColors.textMuted)),
            ],
          ),
          const SizedBox(height: 4),
          FittedBox(
            fit: BoxFit.scaleDown,
            alignment: Alignment.centerLeft,
            child: Text(
              value,
              style: const TextStyle(
                fontSize: 18,
                fontWeight: FontWeight.w700,
                color: AppColors.charcoal,
                fontFeatures: kTabularFigures,
              ),
            ),
          ),
        ],
      );
}

/// The honest caveat on the figures above: this much has not reached the office yet, so it is not
/// in them. It sits on the card it qualifies instead of at the bottom of the screen.
class _PendingFoot extends StatelessWidget {
  const _PendingFoot({required this.pending});

  final int pending;

  @override
  Widget build(BuildContext context) => Semantics(
        button: true,
        child: Pressable(
          onTap: () => Navigator.of(context).push(appRoute<void>((_) => const PendingScreen())),
          child: Container(
            constraints: const BoxConstraints(minHeight: 56),
            padding: const EdgeInsets.symmetric(horizontal: 20, vertical: 10),
            decoration: const BoxDecoration(
              color: AppColors.warningSoft,
              // One pixel inside the card's own corner, so the tint does not cover its border.
              borderRadius: BorderRadius.vertical(bottom: Radius.circular(AppRadius.md - 1)),
            ),
            child: Row(
              children: [
                const Icon(Icons.cloud_upload_outlined, size: 20, color: AppColors.warning),
                const SizedBox(width: 12),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    mainAxisSize: MainAxisSize.min,
                    children: [
                      Text(
                        '$pending waiting to sync',
                        style: const TextStyle(fontWeight: FontWeight.w700, color: AppColors.warning),
                      ),
                      const Text(
                        'Saved here. Not in these figures yet.',
                        style: TextStyle(fontSize: 12.5, color: AppColors.textMuted),
                      ),
                    ],
                  ),
                ),
                const Icon(Icons.chevron_right, color: AppColors.warning),
              ],
            ),
          ),
        ),
      );
}

/// The two things a salesperson does from Home, side by side and each a full thumb wide. Gold is
/// spent here and nowhere else on the screen, so the bill - the thing done forty times a day - is
/// the first thing the eye finds.
class _QuickActions extends StatelessWidget {
  const _QuickActions({required this.onNewBill, required this.onReceivePayment});

  final VoidCallback onNewBill;
  final VoidCallback onReceivePayment;

  @override
  Widget build(BuildContext context) => Padding(
        padding: const EdgeInsets.only(top: 12),
        child: IntrinsicHeight(
          child: Row(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              Expanded(
                child: _ActionCard(
                  icon: Icons.add,
                  title: 'New Bill',
                  subtitle: 'Record a shop sale',
                  primary: true,
                  onTap: onNewBill,
                ),
              ),
              const SizedBox(width: 12),
              Expanded(
                child: _ActionCard(
                  icon: Icons.payments_outlined,
                  title: 'Receive Payment',
                  subtitle: 'Collect from a shop',
                  primary: false,
                  onTap: onReceivePayment,
                ),
              ),
            ],
          ),
        ),
      );
}

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
    return Semantics(
      button: true,
      child: Pressable(
        onTap: onTap,
        child: Container(
          constraints: const BoxConstraints(minHeight: 112),
          decoration: BoxDecoration(
            color: primary ? AppColors.gold : AppColors.surface,
            borderRadius: BorderRadius.circular(AppRadius.md),
            border: primary ? null : Border.all(color: AppColors.border),
          ),
          padding: const EdgeInsets.all(16),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              Container(
                width: 40,
                height: 40,
                alignment: Alignment.center,
                decoration: BoxDecoration(
                  color: primary ? AppColors.charcoal : AppColors.goldSoft,
                  shape: BoxShape.circle,
                ),
                child: Icon(icon, size: 22, color: primary ? AppColors.gold : AppColors.goldDark),
              ),
              const SizedBox(height: 14),
              Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                mainAxisSize: MainAxisSize.min,
                children: [
                  // Charcoal on gold, not white: white on this gold is about 2:1 and washes out
                  // in sunlight; charcoal is about 7:1.
                  // On a narrow phone the title shrinks a little rather than breaking in two,
                  // which would make one card's label taller than its neighbour's. The line's
                  // height follows the user's font size setting.
                  SizedBox(
                    height: MediaQuery.textScalerOf(context).scale(24),
                    child: FittedBox(
                      fit: BoxFit.scaleDown,
                      alignment: Alignment.centerLeft,
                      child: Text(
                        title,
                        style: const TextStyle(
                          color: AppColors.charcoal,
                          fontSize: 16.5,
                          fontWeight: FontWeight.w700,
                        ),
                      ),
                    ),
                  ),
                  const SizedBox(height: 2),
                  Text(
                    subtitle,
                    maxLines: 2,
                    overflow: TextOverflow.ellipsis,
                    style: TextStyle(
                      color: primary ? const Color(0xFF4F3C10) : AppColors.textMuted,
                      fontSize: 13,
                    ),
                  ),
                ],
              ),
            ],
          ),
        ),
      ),
    );
  }
}

/// How far through the shops the day has got. The bar appears only when the phone knows how many
/// shops there are; without that it is a count and nothing more.
class _ShopsVisitedCard extends StatelessWidget {
  const _ShopsVisitedCard({required this.visited, required this.total});

  final int visited;
  final int total;

  @override
  Widget build(BuildContext context) => Padding(
        padding: const EdgeInsets.only(top: 12),
        child: AppCard(
          padding: const EdgeInsets.fromLTRB(20, 16, 20, 18),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Row(
                crossAxisAlignment: CrossAxisAlignment.baseline,
                textBaseline: TextBaseline.alphabetic,
                children: [
                  const Expanded(child: Text('SHOPS VISITED', style: kEyebrowStyle)),
                  Text.rich(
                    TextSpan(
                      children: [
                        TextSpan(
                          text: '$visited',
                          style: const TextStyle(
                            fontSize: 20,
                            fontWeight: FontWeight.w700,
                            color: AppColors.charcoal,
                          ),
                        ),
                        if (total > 0)
                          TextSpan(
                            text: ' / $total',
                            style: const TextStyle(fontSize: 14, color: AppColors.textMuted),
                          ),
                      ],
                    ),
                    style: const TextStyle(fontFeatures: kTabularFigures),
                  ),
                ],
              ),
              if (total > 0) ...[
                const SizedBox(height: 12),
                _Bar(value: (visited / total).clamp(0, 1).toDouble()),
              ],
            ],
          ),
        ),
      );
}

/// A plain progress bar. It shows a measurement, so it does not animate into place: a bar that
/// grows on every visit to Home reads as something happening when nothing is.
class _Bar extends StatelessWidget {
  const _Bar({required this.value});

  final double value;

  @override
  Widget build(BuildContext context) => ClipRRect(
        borderRadius: BorderRadius.circular(AppRadius.pill),
        child: LinearProgressIndicator(
          value: value,
          minHeight: 8,
          backgroundColor: AppColors.surfaceAlt,
          valueColor: const AlwaysStoppedAnimation(AppColors.gold),
        ),
      );
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

/// What is left on the van, at a glance - the same figures as the Van tab. Needs signal like the
/// Van tab does; on a stale phone it simply says so and stays out of the way rather than blocking
/// the rest of the home screen.
class _VanSummaryCard extends ConsumerStatefulWidget {
  const _VanSummaryCard({super.key});

  @override
  ConsumerState<_VanSummaryCard> createState() => _VanSummaryCardState();
}

class _VanSummaryCardState extends ConsumerState<_VanSummaryCard> {
  late final Future<Map<String, dynamic>> _future = ref.read(syncProvider).vanStock();

  void _openVan() => Navigator.of(context).push(appRoute<void>((_) => const VanScreen()));

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.only(top: 12),
      child: Semantics(
        button: true,
        child: Pressable(
          onTap: _openVan,
          child: AppCard(
            padding: const EdgeInsets.fromLTRB(20, 16, 12, 18),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                const Row(
                  children: [
                    Expanded(child: Text('VAN STOCK', style: kEyebrowStyle)),
                    Icon(Icons.chevron_right, size: 20, color: AppColors.textMuted),
                  ],
                ),
                const SizedBox(height: 10),
                Padding(
                  padding: const EdgeInsets.only(right: 8),
                  // The card holds its height while the office answers, so the bills under it do
                  // not jump when the figures arrive.
                  child: ConstrainedBox(
                    constraints: const BoxConstraints(minHeight: 70),
                    child: FutureBuilder<Map<String, dynamic>>(
                      future: _future,
                      builder: (context, snapshot) => AnimatedSwitcher(
                        duration: AppMotion.normal,
                        switchInCurve: _easeOut,
                        layoutBuilder: (current, previous) => Stack(
                          alignment: Alignment.centerLeft,
                          children: [...previous, ?current],
                        ),
                        child: _body(snapshot),
                      ),
                    ),
                  ),
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }

  Widget _body(AsyncSnapshot<Map<String, dynamic>> snapshot) {
    if (snapshot.connectionState != ConnectionState.done) {
      return const _VanPlaceholder(key: ValueKey('loading'));
    }

    if (snapshot.hasError) {
      final offline = snapshot.error is OfflineException;

      return Row(
        key: const ValueKey('error'),
        children: [
          const Icon(Icons.local_shipping_outlined, color: AppColors.textMuted),
          const SizedBox(width: 12),
          Expanded(
            child: Text(
              offline ? 'No connection. Open Van for the last figures.' : 'Could not load van stock.',
              style: const TextStyle(color: AppColors.textMuted, height: 1.35),
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
      key: const ValueKey('figures'),
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Row(
          children: [
            Expanded(child: _VanFigure(label: 'Loaded', value: quantity(loaded))),
            Expanded(child: _VanFigure(label: 'Sold', value: quantity(sold))),
            Expanded(child: _VanFigure(label: 'Remaining', value: quantity(remaining))),
          ],
        ),
        const SizedBox(height: 14),
        _Bar(value: progress),
      ],
    );
  }
}

/// Quiet blocks where the van figures will be. Still, not shimmering: it is on screen for a
/// moment, and movement there would pull the eye away from the sales figure.
class _VanPlaceholder extends StatelessWidget {
  const _VanPlaceholder({super.key});

  @override
  Widget build(BuildContext context) {
    Widget block(double width, double height) => Container(
          width: width,
          height: height,
          decoration: BoxDecoration(
            color: AppColors.surfaceAlt,
            borderRadius: BorderRadius.circular(AppRadius.sm),
          ),
        );

    return ExcludeSemantics(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              for (var i = 0; i < 3; i++)
                Expanded(child: Align(alignment: Alignment.centerLeft, child: block(56, 40))),
            ],
          ),
          const SizedBox(height: 14),
          block(double.infinity, 8),
        ],
      ),
    );
  }
}

class _VanFigure extends StatelessWidget {
  const _VanFigure({required this.label, required this.value});

  final String label;
  final String value;

  @override
  Widget build(BuildContext context) => Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          FittedBox(
            fit: BoxFit.scaleDown,
            alignment: Alignment.centerLeft,
            child: Text(
              value,
              style: const TextStyle(
                fontSize: 20,
                fontWeight: FontWeight.w700,
                color: AppColors.charcoal,
                fontFeatures: kTabularFigures,
              ),
            ),
          ),
          const SizedBox(height: 2),
          Text(label, style: const TextStyle(fontSize: 13, color: AppColors.textMuted)),
        ],
      );
}

class _NothingYet extends StatelessWidget {
  const _NothingYet();

  @override
  Widget build(BuildContext context) => const AppCard(
        padding: EdgeInsets.fromLTRB(20, 18, 20, 18),
        child: Row(
          children: [
            Icon(Icons.today_outlined, color: AppColors.textMuted),
            SizedBox(width: 14),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text('No summary yet',
                      style: TextStyle(fontWeight: FontWeight.w700, color: AppColors.charcoal)),
                  SizedBox(height: 2),
                  Text(
                    'Sync once with a connection and the day so far will be here.',
                    style: TextStyle(fontSize: 13, color: AppColors.textMuted, height: 1.35),
                  ),
                ],
              ),
            ),
          ],
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
            SectionHeader(
              "Today's bills",
              trailing: Text('${today.length}', style: kEyebrowStyle),
            ),
            AppCard(
              padding: EdgeInsets.zero,
              child: Column(
                children: [
                  for (final (index, entry) in today.indexed) ...[
                    if (index > 0) const Divider(indent: 64),
                    _BillRow(entry: entry),
                  ],
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
    final (icon, colour, tint, detail) = switch (entry.status) {
      'Synced' => (
          Icons.check,
          AppColors.success,
          AppColors.successSoft,
          entry.documentNumber == null ? 'With the office · $time' : '${entry.documentNumber} · $time',
        ),
      'Failed' => (
          Icons.priority_high,
          AppColors.danger,
          AppColors.dangerSoft,
          entry.lastError ?? 'The office refused this.',
        ),
      _ => (Icons.schedule, AppColors.textMuted, AppColors.surfaceAlt, 'Waiting to sync · $time'),
    };
    final failed = entry.status == 'Failed';

    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 12),
      child: Row(
        children: [
          Container(
            width: 36,
            height: 36,
            alignment: Alignment.center,
            decoration: BoxDecoration(color: tint, shape: BoxShape.circle),
            child: Icon(icon, size: 18, color: colour),
          ),
          const SizedBox(width: 12),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  entry.summary,
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                  style: const TextStyle(fontSize: 15, fontWeight: FontWeight.w600),
                ),
                const SizedBox(height: 2),
                Text(
                  detail,
                  maxLines: failed ? 3 : 1,
                  overflow: TextOverflow.ellipsis,
                  style: TextStyle(
                    fontSize: 13,
                    color: failed ? AppColors.danger : AppColors.textMuted,
                    fontFeatures: kTabularFigures,
                  ),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}
