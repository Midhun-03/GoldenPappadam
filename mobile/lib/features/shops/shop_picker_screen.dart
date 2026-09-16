import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../app.dart';
import '../../core/money.dart';
import '../../data/local/database.dart';
import '../../data/sales_repository.dart';

/// Which shop the bill is for.
///
/// The same search the Shops tab uses, in a screen that hands one back rather than opening it.
/// Shops the salesperson has already dealt with today come first, because on a route the next bill
/// is usually for somewhere they have just been.
class ShopPickerScreen extends ConsumerStatefulWidget {
  const ShopPickerScreen({super.key});

  @override
  ConsumerState<ShopPickerScreen> createState() => _ShopPickerScreenState();
}

class _ShopPickerScreenState extends ConsumerState<ShopPickerScreen> {
  final _search = TextEditingController();
  String _term = '';
  List<String> _recentNames = const [];

  @override
  void initState() {
    super.initState();
    ref.read(databaseProvider).recentShopNames().then((names) {
      if (mounted) setState(() => _recentNames = names);
    });
  }

  @override
  void dispose() {
    _search.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final db = ref.watch(databaseProvider);
    final searching = _term.trim().isNotEmpty;

    return Scaffold(
      appBar: AppBar(title: const Text('Which shop?')),
      body: Column(
        children: [
          Padding(
            padding: const EdgeInsets.fromLTRB(12, 12, 12, 8),
            child: TextField(
              controller: _search,
              autofocus: true,
              onChanged: (value) => setState(() => _term = value),
              textInputAction: TextInputAction.search,
              decoration: InputDecoration(
                hintText: 'Find a shop',
                prefixIcon: const Icon(Icons.search),
                suffixIcon: searching
                    ? IconButton(
                        icon: const Icon(Icons.clear),
                        onPressed: () {
                          _search.clear();
                          setState(() => _term = '');
                        },
                      )
                    : null,
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
                  return Center(
                    child: Padding(
                      padding: const EdgeInsets.all(32),
                      child: Text(
                        searching
                            ? 'No shop by that name. Try part of it, or the phone number.'
                            : 'No shops yet. Sync once with a connection.',
                        textAlign: TextAlign.center,
                      ),
                    ),
                  );
                }

                // Only worth a "recent" section when not searching: once they are typing, the
                // search itself is the shortcut.
                final recent = searching
                    ? const <CachedCustomer>[]
                    : _recentNames
                        .map((name) => shops.where((shop) => shop.name == name).firstOrNull)
                        .whereType<CachedCustomer>()
                        .toList();

                final rest = shops.where((shop) => !recent.contains(shop)).toList();

                return ListView(
                  children: [
                    if (recent.isNotEmpty) ...[
                      const _SectionLabel('Recently'),
                      for (final shop in recent) _ShopTile(shop: shop),
                      const _SectionLabel('All shops'),
                    ],
                    for (final shop in rest) _ShopTile(shop: shop),
                  ],
                );
              },
            ),
          ),
        ],
      ),
    );
  }
}

class _SectionLabel extends StatelessWidget {
  const _SectionLabel(this.text);

  final String text;

  @override
  Widget build(BuildContext context) => Padding(
        padding: const EdgeInsets.fromLTRB(16, 12, 16, 4),
        child: Text(
          text,
          style: TextStyle(
            fontSize: 12,
            fontWeight: FontWeight.w500,
            color: Theme.of(context).hintColor,
          ),
        ),
      );
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
      onTap: () => Navigator.of(context).pop(shop),
    );
  }
}
