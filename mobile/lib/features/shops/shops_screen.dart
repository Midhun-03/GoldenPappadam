import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../app.dart';
import '../../core/money.dart';
import '../../core/theme.dart';
import '../../core/widgets.dart';
import '../../data/local/database.dart';
import '../../data/sales_repository.dart';
import '../sync/sync_views.dart';
import 'shop_form_screen.dart';
import 'shop_screen.dart';

/// The list the salesperson opens on. Search first, because on a route of fifteen shops the
/// fastest way to the right one is to type three letters of its name.
///
/// New shop is always one tap away, and under a search the typed name can be added directly. Either
/// way the form checks the name against the shops the phone knows, so one the office already has is
/// opened rather than entered twice - and a name containing a known shop's ("Danya Supermarket
/// Coimbatore") is pointed at that shop, to be added as its branch.
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
      floatingActionButton: FloatingActionButton.extended(
        key: const ValueKey('new-shop'),
        onPressed: () => Navigator.of(context).push(appRoute<void>(
            (_) => ShopFormScreen(initialName: _term.trim().isEmpty ? null : _term.trim()))),
        icon: const Icon(Icons.add_business_outlined),
        label: const Text('New shop'),
      ),
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
                    final searching = _term.trim().isNotEmpty;

                    if (shops.isEmpty) {
                      return RefreshIndicator(
                        onRefresh: () => ref.read(syncProvider).syncNow(),
                        child: ListView(
                          children: [
                            _EmptyShops(searching: searching),
                            if (searching) _AddShop(term: _term),
                          ],
                        ),
                      );
                    }

                    return RefreshIndicator(
                      onRefresh: () => ref.read(syncProvider).syncNow(),
                      child: ListView.separated(
                        padding: const EdgeInsets.only(bottom: 8),
                        itemCount: shops.length + 1,
                        separatorBuilder: (_, _) => const Divider(height: 1),
                        itemBuilder: (context, index) => index == shops.length
                            ? (searching
                                ? _AddShop(term: _term)
                                // Room for the New shop button, so it never covers the last shop.
                                : const SizedBox(height: 72))
                            : _ShopTile(
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

/// Under a search: add what was typed as a new shop - unless it already is one, or looks like a
/// branch of one.
class _AddShop extends ConsumerWidget {
  const _AddShop({required this.term});

  final String term;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final repository = ref.watch(shopsRepositoryProvider);

    return FutureBuilder<(CachedCustomer?, CachedCustomer?)>(
      future: (() async => (await repository.findByName(term), await repository.likelyParentOf(term)))(),
      builder: (context, snapshot) {
        final data = snapshot.data;
        if (data == null) return const SizedBox.shrink();

        final (exact, parent) = data;

        // It is already in the list above. Nothing to add.
        if (exact != null) return const SizedBox.shrink();

        void addNew() => Navigator.of(context)
            .push(appRoute<void>((_) => ShopFormScreen(initialName: term.trim())));

        return Padding(
          padding: const EdgeInsets.fromLTRB(12, 12, 12, 16),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              if (parent != null) ...[
                AppCard(
                  color: AppColors.warningSoft,
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.stretch,
                    children: [
                      Text('Is this a branch of ${parent.name}?',
                          style: const TextStyle(fontWeight: FontWeight.w700)),
                      const SizedBox(height: 4),
                      const Text('Another shop of a customer we know is added as its branch, '
                          'not as a new customer.'),
                      const SizedBox(height: 10),
                      FilledButton(
                        key: const ValueKey('open-parent'),
                        onPressed: () => Navigator.of(context)
                            .push(appRoute<void>((_) => ShopScreen(shopId: parent.id))),
                        child: Text('Open ${parent.name}'),
                      ),
                    ],
                  ),
                ),
                const SizedBox(height: 8),
              ],
              OutlinedButton.icon(
                key: const ValueKey('add-new-shop'),
                onPressed: addNew,
                icon: const Icon(Icons.add_business_outlined, size: 18),
                label: Text(parent == null
                    ? 'Add "${term.trim()}" as a new shop'
                    : 'No, "${term.trim()}" is a different shop'),
              ),
            ],
          ),
        );
      },
    );
  }
}
