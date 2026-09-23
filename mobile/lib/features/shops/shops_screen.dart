import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../app.dart';
import '../../core/money.dart';
import '../../core/theme.dart';
import '../../core/widgets.dart';
import '../../data/local/database.dart';
import '../../data/sales_repository.dart';
import '../sync/sync_views.dart';
import 'shop_screen.dart';

/// The list the salesperson opens on. Search first, because on a route of fifteen shops the
/// fastest way to the right one is to type three letters of its name.
class ShopsScreen extends ConsumerStatefulWidget {
  const ShopsScreen({super.key});

  @override
  ConsumerState<ShopsScreen> createState() => _ShopsScreenState();
}

class _ShopsScreenState extends ConsumerState<ShopsScreen> {
  final _search = TextEditingController();
  String _term = '';

  @override
  void dispose() {
    _search.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final db = ref.watch(databaseProvider);

    return Scaffold(
      appBar: AppBar(title: const Text('Shops'), actions: const [PendingBadge()]),
      body: Column(
        children: [
          const SyncBanner(),
          Padding(
            padding: const EdgeInsets.fromLTRB(12, 12, 12, 8),
            child: TextField(
              controller: _search,
              onChanged: (value) => setState(() => _term = value),
              textInputAction: TextInputAction.search,
              decoration: InputDecoration(
                hintText: 'Find a shop',
                prefixIcon: const Icon(Icons.search),
                suffixIcon: _term.isEmpty
                    ? null
                    : IconButton(
                        icon: const Icon(Icons.clear),
                        onPressed: () {
                          _search.clear();
                          setState(() => _term = '');
                        },
                      ),
              ),
            ),
          ),
          // watchOutboxOfType streams every recorded entry so a tile can show "not yet with the
          // office" the moment something is saved, without a fresh query per row.
          Expanded(
            child: StreamBuilder<List<OutboxEntry>>(
              stream: db.watchUnfinished(),
              builder: (context, unfinished) {
                final pendingByShop = <String, int>{};
                for (final entry in unfinished.data ?? const <OutboxEntry>[]) {
                  final name = entry.summary.split('·').first.trim();
                  if (name.isEmpty) continue;
                  pendingByShop[name] = (pendingByShop[name] ?? 0) + 1;
                }

                return FutureBuilder<List<CachedCustomer>>(
                  future: db.searchShops(_term),
                  builder: (context, snapshot) {
                    if (snapshot.connectionState == ConnectionState.waiting && !snapshot.hasData) {
                      return const Center(child: CircularProgressIndicator());
                    }

                    final shops = snapshot.data ?? const <CachedCustomer>[];

                    if (shops.isEmpty) {
                      return RefreshIndicator(
                        onRefresh: () => ref.read(syncProvider).syncNow(),
                        child: ListView(
                          children: [_EmptyShops(searching: _term.isNotEmpty)],
                        ),
                      );
                    }

                    return RefreshIndicator(
                      onRefresh: () => ref.read(syncProvider).syncNow(),
                      child: ListView.separated(
                        padding: const EdgeInsets.only(bottom: 8),
                        itemCount: shops.length,
                        separatorBuilder: (_, _) => const Divider(height: 1),
                        itemBuilder: (context, index) => _ShopTile(
                          shop: shops[index],
                          pending: pendingByShop[shops[index].name] ?? 0,
                        ),
                      ),
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
}

class _ShopTile extends StatelessWidget {
  const _ShopTile({required this.shop, required this.pending});

  final CachedCustomer shop;
  final int pending;

  @override
  Widget build(BuildContext context) {
    final owes = shop.balance > 0;

    return ListTile(
      leading: CircleAvatar(
        backgroundColor: AppColors.surfaceAlt,
        foregroundColor: AppColors.charcoal,
        child: Text(
          shop.name.isEmpty ? '?' : shop.name[0].toUpperCase(),
          style: const TextStyle(fontWeight: FontWeight.w700),
        ),
      ),
      title: Text(shop.name, style: const TextStyle(fontWeight: FontWeight.w600)),
      subtitle: !shop.hasMultipleBranches && pending == 0
          ? null
          : Padding(
              padding: const EdgeInsets.only(top: 4),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                mainAxisSize: MainAxisSize.min,
                children: [
                  if (shop.hasMultipleBranches)
                    const Text('Multiple branches',
                        style: TextStyle(fontSize: 12.5, color: AppColors.textMuted)),
                  if (pending > 0) ...[
                    if (shop.hasMultipleBranches) const SizedBox(height: 4),
                    StatusPill('$pending waiting to sync', tone: Tone.warning, icon: Icons.schedule),
                  ],
                ],
              ),
            ),
      trailing: Column(
        mainAxisAlignment: MainAxisAlignment.center,
        crossAxisAlignment: CrossAxisAlignment.end,
        children: [
          Text(
            owes ? money(shop.balance) : 'Clear',
            style: TextStyle(
              fontWeight: FontWeight.w700,
              color: owes ? AppColors.danger : AppColors.success,
            ),
          ),
          const SizedBox(height: 2),
          const Icon(Icons.chevron_right, size: 18, color: AppColors.textMuted),
        ],
      ),
      onTap: () => Navigator.of(context).push(appRoute<void>((_) => ShopScreen(shopId: shop.id))),
    );
  }
}

class _EmptyShops extends StatelessWidget {
  const _EmptyShops({required this.searching});

  final bool searching;

  @override
  Widget build(BuildContext context) => EmptyState(
        icon: searching ? Icons.search_off : Icons.storefront_outlined,
        title: searching ? 'No shop by that name' : 'No shops yet',
        message: searching
            ? 'Try part of the name, or the phone number.'
            : 'Sync once with a connection and the shop list is yours, signal or not.',
      );
}
