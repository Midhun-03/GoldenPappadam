import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../app.dart';
import '../../core/money.dart';
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
            padding: const EdgeInsets.only(bottom: 24),
            children: [
              _BalanceCard(shop: shop),
              if ((shop.phone ?? '').isNotEmpty || (shop.address ?? '').isNotEmpty)
                Padding(
                  padding: const EdgeInsets.fromLTRB(16, 4, 16, 8),
                  child: Text(
                    [shop.contactPerson, shop.phone, shop.address]
                        .where((part) => (part ?? '').isNotEmpty)
                        .join(' · '),
                    style: TextStyle(color: Theme.of(context).hintColor),
                  ),
                ),
              Padding(
                padding: const EdgeInsets.fromLTRB(16, 8, 16, 0),
                child: FilledButton.icon(
                  icon: const Icon(Icons.add_shopping_cart),
                  label: const Text('Record a delivery'),
                  onPressed: () => _open(SaleScreen(shop: shop)),
                ),
              ),
              Padding(
                padding: const EdgeInsets.fromLTRB(16, 10, 16, 0),
                child: OutlinedButton.icon(
                  icon: const Icon(Icons.payments_outlined),
                  label: const Text('Collect a payment'),
                  onPressed: () => _open(PaymentScreen(shop: shop)),
                  style: OutlinedButton.styleFrom(minimumSize: const Size.fromHeight(52)),
                ),
              ),
              Padding(
                padding: const EdgeInsets.fromLTRB(16, 10, 16, 0),
                child: TextButton(
                  onPressed: () => _recordNoOrder(shop),
                  child: const Text('Visited, nothing needed'),
                ),
              ),
              const SizedBox(height: 8),
              _TodayHere(shopName: shop.name),
              _PricesHere(shopId: shop.id),
            ],
          ),
        );
      },
    );
  }

  Future<void> _open(Widget screen) async {
    await Navigator.of(context).push(MaterialPageRoute<void>(builder: (_) => screen));

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
    final scheme = Theme.of(context).colorScheme;

    return Container(
      width: double.infinity,
      margin: const EdgeInsets.fromLTRB(16, 16, 16, 8),
      padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(
        color: owes ? scheme.errorContainer : scheme.surfaceContainerHighest,
        borderRadius: BorderRadius.circular(12),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(
            owes ? 'Outstanding' : 'Nothing outstanding',
            style: TextStyle(color: owes ? scheme.onErrorContainer : scheme.onSurfaceVariant),
          ),
          const SizedBox(height: 4),
          Text(
            money(shop.balance),
            style: Theme.of(context).textTheme.headlineMedium?.copyWith(
                  fontWeight: FontWeight.w600,
                  color: owes ? scheme.onErrorContainer : scheme.onSurfaceVariant,
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
                color: owes ? scheme.onErrorContainer : scheme.onSurfaceVariant,
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
            const Padding(
              padding: EdgeInsets.fromLTRB(16, 16, 16, 4),
              child: Text('Recorded here, not yet with the office',
                  style: TextStyle(fontWeight: FontWeight.w500)),
            ),
            for (final entry in mine)
              ListTile(
                dense: true,
                leading: Icon(
                  entry.status == 'Failed' ? Icons.error_outline : Icons.schedule,
                  color: entry.status == 'Failed' ? Theme.of(context).colorScheme.error : null,
                ),
                title: Text(entry.summary),
                subtitle: Text(entry.status == 'Failed'
                    ? entry.lastError ?? 'The office refused this.'
                    : timeOfDay(entry.recordedAt)),
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
            const Padding(
              padding: EdgeInsets.fromLTRB(16, 20, 16, 4),
              child: Text('What this shop pays', style: TextStyle(fontWeight: FontWeight.w500)),
            ),
            for (final product in priced)
              ListTile(
                dense: true,
                title: Text(product.name),
                trailing: Text(
                  money(prices[product.id]!),
                  style: const TextStyle(fontWeight: FontWeight.w500),
                ),
              ),
          ],
        );
      },
    );
  }
}
