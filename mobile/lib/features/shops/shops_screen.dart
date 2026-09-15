import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../app.dart';
import '../../core/money.dart';
import '../../data/local/database.dart';
import '../../data/sales_repository.dart';
import '../home/home_screen.dart';
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
      appBar: AppBar(
        title: const Text('Shops'),
        actions: [
          IconButton(
            tooltip: 'Sync now',
            icon: const Icon(Icons.sync),
            onPressed: () => ref.read(syncProvider).syncNow(),
          ),
        ],
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
          Expanded(
            child: FutureBuilder<List<CachedCustomer>>(
              future: db.searchShops(_term),
              builder: (context, snapshot) {
                if (snapshot.connectionState == ConnectionState.waiting && !snapshot.hasData) {
                  return const Center(child: CircularProgressIndicator());
                }

                final shops = snapshot.data ?? const <CachedCustomer>[];

                if (shops.isEmpty) {
                  return _EmptyShops(searching: _term.isNotEmpty);
                }

                return ListView.separated(
                  itemCount: shops.length,
                  separatorBuilder: (_, _) => const Divider(height: 1),
                  itemBuilder: (context, index) => _ShopTile(shop: shops[index]),
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
  const _ShopTile({required this.shop});

  final CachedCustomer shop;

  @override
  Widget build(BuildContext context) {
    final owes = shop.balance > 0;

    return ListTile(
      title: Text(shop.name, style: const TextStyle(fontWeight: FontWeight.w500)),
      subtitle: Text(
        owes ? 'Owes ${money(shop.balance)}' : 'Nothing outstanding',
        style: TextStyle(
          color: owes ? Theme.of(context).colorScheme.error : Theme.of(context).hintColor,
        ),
      ),
      trailing: const Icon(Icons.chevron_right),
      onTap: () => Navigator.of(context).push(
        MaterialPageRoute<void>(builder: (_) => ShopScreen(shopId: shop.id)),
      ),
    );
  }
}

class _EmptyShops extends StatelessWidget {
  const _EmptyShops({required this.searching});

  final bool searching;

  @override
  Widget build(BuildContext context) => Center(
        child: Padding(
          padding: const EdgeInsets.all(32),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              Icon(searching ? Icons.search_off : Icons.storefront_outlined, size: 40),
              const SizedBox(height: 12),
              Text(
                searching ? 'No shop by that name' : 'No shops yet',
                style: Theme.of(context).textTheme.titleMedium,
              ),
              const SizedBox(height: 4),
              Text(
                searching
                    ? 'Try part of the name, or the phone number.'
                    : 'Sync once with a connection and the shop list is yours, signal or not.',
                textAlign: TextAlign.center,
              ),
            ],
          ),
        ),
      );
}
