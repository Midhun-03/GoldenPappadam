import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../app.dart';
import '../../core/money.dart';
import '../../core/theme.dart';
import '../../core/widgets.dart';
import '../../data/local/database.dart';
import '../payment/payment_screen.dart';
import '../sale/sale_screen.dart';

/// One shop, with the three things the salesperson needs before deciding anything: who it is,
/// what it owes, and what they have already recorded here today.
class ShopScreen extends ConsumerStatefulWidget {
  const ShopScreen({required this.shopId, super.key});

  final String shopId;

  @override
  ConsumerState<ShopScreen> createState() => _ShopScreenState();
}

class _ShopScreenState extends ConsumerState<ShopScreen> {
  @override
  Widget build(BuildContext context) {
    final db = ref.watch(databaseProvider);

    return FutureBuilder<CachedCustomer?>(
      future: db.findCustomer(widget.shopId),
      builder: (context, snapshot) {
        final shop = snapshot.data;

        if (shop == null) {
          return const Scaffold(body: Center(child: CircularProgressIndicator()));
        }

        return Scaffold(
          appBar: AppBar(title: Text(shop.name)),
          body: ListView(
            padding: const EdgeInsets.fromLTRB(12, 4, 12, 24),
            children: [
              _BalanceCard(shop: shop),
              Padding(
                padding: const EdgeInsets.fromLTRB(4, 10, 4, 0),
                child: Align(alignment: Alignment.centerLeft, child: BillKindPill(gstin: shop.gstin)),
              ),
              if ((shop.phone ?? '').isNotEmpty || (shop.address ?? '').isNotEmpty)
                Padding(
                  padding: const EdgeInsets.fromLTRB(4, 10, 4, 4),
                  child: Text(
                    [shop.contactPerson, shop.phone, shop.address]
                        .where((part) => (part ?? '').isNotEmpty)
                        .join(' · '),
                    style: const TextStyle(color: AppColors.textMuted),
                  ),
                ),
              const SizedBox(height: 10),
              Row(
                children: [
                  Expanded(
                    child: FilledButton.icon(
                      icon: const Icon(Icons.add, size: 18),
                      label: const Text('Record sale'),
                      onPressed: () => _open(SaleScreen(shop: shop)),
                    ),
                  ),
                  const SizedBox(width: 10),
                  Expanded(
                    child: OutlinedButton.icon(
                      icon: const Icon(Icons.payments_outlined, size: 18),
                      label: const Text('Collect'),
                      onPressed: () => _open(PaymentScreen(shop: shop)),
                    ),
                  ),
                ],
              ),
              const SizedBox(height: 4),
              TextButton(
                onPressed: () => _recordNoOrder(shop),
                child: const Text('Visited, nothing needed'),
              ),
              _TodayHere(shopName: shop.name),
              _PaymentHistory(shopId: shop.id, shopName: shop.name),
              if (shop.hasMultipleBranches) _BranchesHere(shopId: shop.id),
              _PricesHere(shopId: shop.id),
            ],
          ),
        );
      },
    );
  }

  Future<void> _open(Widget screen) async {
    await Navigator.of(context).push(appRoute<void>((_) => screen));

    // The balance and the list below both change when something is recorded.
    if (mounted) setState(() {});
  }

  Future<void> _recordNoOrder(CachedCustomer shop) async {
    await ref.read(salesRepositoryProvider).recordVisit(
          customerId: shop.id,
          customerName: shop.name,
          outcome: 'NoOrder',
        );

    unawaitedSync(ref);

    if (!mounted) return;

    setState(() {});
    ScaffoldMessenger.of(context).showSnackBar(
      const SnackBar(content: Text('Visit recorded.')),
    );
  }
}

class _BalanceCard extends ConsumerWidget {
  const _BalanceCard({required this.shop});

  final CachedCustomer shop;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final owes = shop.balance > 0;

    return AppCard(
      margin: const EdgeInsets.only(top: 8),
      color: owes ? AppColors.dangerSoft : AppColors.successSoft,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(
            owes ? 'Outstanding balance' : 'Nothing outstanding',
            style: TextStyle(
              color: owes ? AppColors.danger : AppColors.success,
              fontWeight: FontWeight.w600,
            ),
          ),
          const SizedBox(height: 4),
          Text(
            money(shop.balance),
            style: Theme.of(context).textTheme.headlineMedium?.copyWith(
                  color: owes ? AppColors.danger : AppColors.success,
                ),
          ),
          const SizedBox(height: 6),
          // The figure is as good as the last sync and says so, because on the road it may be
          // hours old and the salesperson is about to have a conversation about money.
          FutureBuilder<DateTime?>(
            future: ref.read(syncProvider).lastSyncedAt,
            builder: (context, snapshot) => Text(
              'as of ${howLongAgo(snapshot.data)}',
              style: TextStyle(
                fontSize: 12,
                color: (owes ? AppColors.danger : AppColors.success).withValues(alpha: 0.8),
              ),
            ),
          ),
        ],
      ),
    );
  }
}

/// What has been recorded here today but not yet confirmed by the office.
class _TodayHere extends ConsumerWidget {
  const _TodayHere({required this.shopName});

  final String shopName;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    return StreamBuilder<List<OutboxEntry>>(
      stream: ref.watch(databaseProvider).watchUnfinished(),
      builder: (context, snapshot) {
        final mine = (snapshot.data ?? const <OutboxEntry>[])
            .where((entry) => entry.summary.startsWith('$shopName ·') && entry.type != 'Visit')
            .toList();

        if (mine.isEmpty) return const SizedBox.shrink();

        return Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            const SectionHeader('Recorded here, not yet with the office'),
            AppCard(
              padding: EdgeInsets.zero,
              child: Column(
                children: [
                  for (final entry in mine)
                    ListTile(
                      dense: true,
                      leading: Icon(
                        entry.status == 'Failed' ? Icons.error_outline : Icons.schedule,
                        color: entry.status == 'Failed' ? AppColors.danger : AppColors.textMuted,
                      ),
                      title: Text(entry.summary),
                      subtitle: Text(entry.status == 'Failed'
                          ? entry.lastError ?? 'The office refused this.'
                          : timeOfDay(entry.recordedAt)),
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

/// Money received from this shop. The office's record, cached at the last sync, with anything this
/// phone has just taken shown above it as still waiting - so a payment collected two minutes ago is
/// visible immediately rather than seeming to have vanished.
class _PaymentHistory extends ConsumerWidget {
  const _PaymentHistory({required this.shopId, required this.shopName});

  final String shopId;
  final String shopName;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final db = ref.watch(databaseProvider);

    return StreamBuilder<List<OutboxEntry>>(
      stream: db.watchOutboxOfType('Payment'),
      builder: (context, outbox) {
        final unsent = (outbox.data ?? const <OutboxEntry>[])
            .where((entry) =>
                entry.summary.startsWith('\$shopName ·') && entry.status != 'Synced')
            .toList();

        return FutureBuilder<List<CachedPayment>>(
          future: db.paymentsFor(shopId),
          builder: (context, snapshot) {
            final settled = snapshot.data ?? const <CachedPayment>[];

            if (settled.isEmpty && unsent.isEmpty) return const SizedBox.shrink();

            return Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                const SectionHeader('Payment history'),
                AppCard(
                  padding: EdgeInsets.zero,
                  child: Column(
                    children: [
                      for (final entry in unsent)
                        ListTile(
                          dense: true,
                          leading: Icon(
                            entry.status == 'Failed' ? Icons.error_outline : Icons.schedule,
                            color: entry.status == 'Failed' ? AppColors.danger : AppColors.textMuted,
                          ),
                          title: Text(entry.summary.split('·').last.trim()),
                          subtitle: Text(entry.status == 'Failed'
                              ? entry.lastError ?? 'The office refused this.'
                              : 'Waiting to go up · \${timeOfDay(entry.recordedAt)}'),
                        ),
                      for (final payment in settled)
                        ListTile(
                          dense: true,
                          leading: const Icon(Icons.check_circle_outline, color: AppColors.success),
                          title: Text(money(payment.amount)),
                          subtitle: Text([
                            dayAndTime(payment.recordedAt),
                            payment.method,
                            if ((payment.reference ?? '').isNotEmpty) payment.reference!,
                            if ((payment.notes ?? '').isNotEmpty) payment.notes!,
                          ].join(' · ')),
                        ),
                    ],
                  ),
                ),
              ],
            );
          },
        );
      },
    );
  }
}

/// The physical shops under a multi-branch customer, e.g. Kundara under Danya Supermarket - a
/// reminder of which branches exist before the salesperson opens Record sale and has to pick one.
class _BranchesHere extends ConsumerWidget {
  const _BranchesHere({required this.shopId});

  final String shopId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final db = ref.watch(databaseProvider);

    return FutureBuilder<List<CachedBranch>>(
      future: db.branchesFor(shopId),
      builder: (context, snapshot) {
        final branches = snapshot.data ?? const <CachedBranch>[];
        if (branches.isEmpty) return const SizedBox.shrink();

        return Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            const SectionHeader('Branches'),
            AppCard(
              padding: EdgeInsets.zero,
              child: Column(
                children: [
                  for (final branch in branches)
                    ListTile(
                      dense: true,
                      leading: const Icon(Icons.storefront_outlined, color: AppColors.textMuted),
                      title: Text(branch.name),
                      subtitle: (branch.location ?? '').isEmpty ? null : Text(branch.location!),
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

/// What this shop pays. Read-only, and deliberately so: only the office sets a price, and the app
/// does not even have a field to type one into.
class _PricesHere extends ConsumerWidget {
  const _PricesHere({required this.shopId});

  final String shopId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final db = ref.watch(databaseProvider);

    return FutureBuilder<(Map<String, double?>, List<CachedProduct>)>(
      future: (() async => (await db.pricesFor(shopId), await db.allProducts()))(),
      builder: (context, snapshot) {
        final data = snapshot.data;
        if (data == null) return const SizedBox.shrink();

        final (prices, products) = data;
        final priced = products.where((p) => prices[p.id] != null).toList();

        if (priced.isEmpty) return const SizedBox.shrink();

        return Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            const SectionHeader('What this shop pays'),
            AppCard(
              padding: EdgeInsets.zero,
              child: Column(
                children: [
                  for (final product in priced)
                    ListTile(
                      dense: true,
                      title: Text(product.name),
                      trailing: Text(
                        money(prices[product.id]!),
                        style: const TextStyle(fontWeight: FontWeight.w600),
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
