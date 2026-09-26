import 'dart:async';

import 'package:connectivity_plus/connectivity_plus.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'core/theme.dart';
import 'data/local/database.dart';
import 'data/sales_repository.dart';
import 'data/shops_repository.dart';
import 'data/remote/api_client.dart';
import 'features/auth/login_screen.dart';
import 'features/home/dashboard_screen.dart';
import 'features/more/more_screen.dart';
import 'features/orders/orders_screen.dart';
import 'features/shops/shops_screen.dart';
import 'features/van/van_screen.dart';
import 'sync/sync_engine.dart';

final databaseProvider = Provider<AppDatabase>((ref) {
  final db = AppDatabase();
  ref.onDispose(db.close);

  return db;
});

final apiProvider = Provider<ApiClient>((ref) => ApiClient(
      onSignedOut: () => ref.read(sessionProvider.notifier).set(signedIn: false),
    ));

final syncProvider = Provider<SyncEngine>((ref) {
  final engine = SyncEngine(database: ref.watch(databaseProvider), apiClient: ref.watch(apiProvider));
  ref.onDispose(engine.dispose);

  return engine;
});

final salesRepositoryProvider =
    Provider<SalesRepository>((ref) => SalesRepository(ref.watch(databaseProvider)));

final shopsRepositoryProvider =
    Provider<ShopsRepository>((ref) => ShopsRepository(ref.watch(databaseProvider)));

/// Hands the work to the office without making anyone wait for it. The sale is already saved on
/// the phone by the time this is called, so failure here changes nothing the salesperson can see.
void unawaitedSync(WidgetRef ref) => unawaited(ref.read(syncProvider).syncNow());

/// Whether somebody is signed in. Null while the app is still looking.
class SessionNotifier extends Notifier<bool?> {
  @override
  bool? build() => null;

  void set({required bool signedIn}) => state = signedIn;
}

final sessionProvider = NotifierProvider<SessionNotifier, bool?>(SessionNotifier.new);

/// How many things are waiting or stuck, straight from the outbox, so the count on screen can
/// never drift away from what is actually in the database.
final pendingCountProvider = StreamProvider<int>(
  (ref) => ref.watch(databaseProvider).watchPendingCount(),
);

final syncStatusProvider = StreamProvider<SyncStatus>(
  (ref) => ref.watch(syncProvider).status,
);

/// Watches the radio and syncs the moment there is something to sync over.
final connectivityProvider = Provider<StreamSubscription<List<ConnectivityResult>>>((ref) {
  final sync = ref.watch(syncProvider);

  final subscription = Connectivity().onConnectivityChanged.listen((results) {
    final online = results.any((result) => result != ConnectivityResult.none);

    // Coming back into signal is the moment worth acting on. Losing it needs no action at all:
    // the app keeps working and the outbox keeps the work.
    if (online) unawaited(sync.syncNow());
  });

  ref.onDispose(subscription.cancel);

  return subscription;
});

class GoldenPappadamApp extends ConsumerStatefulWidget {
  const GoldenPappadamApp({super.key});

  @override
  ConsumerState<GoldenPappadamApp> createState() => _GoldenPappadamAppState();
}

class _GoldenPappadamAppState extends ConsumerState<GoldenPappadamApp> {
  @override
  void initState() {
    super.initState();
    unawaited(_restoreSession());
  }

  Future<void> _restoreSession() async {
    final signedIn = await ref.read(apiProvider).hasSession;
    if (!mounted) return;

    ref.read(sessionProvider.notifier).set(signedIn: signedIn);

    if (signedIn) {
      ref.read(connectivityProvider);
      ref.read(syncProvider).start();
    }
  }

  @override
  Widget build(BuildContext context) {
    final session = ref.watch(sessionProvider);

    return MaterialApp(
      title: 'Golden Pappadam',
      debugShowCheckedModeBanner: false,
      theme: AppTheme.light(),
      home: switch (session) {
        null => const Scaffold(body: Center(child: CircularProgressIndicator())),
        true => const SalesHome(),
        false => const LoginScreen(),
      },
    );
  }
}

/// The five things a salesperson does, one tap apart. No drawer and no nesting: on a doorstep,
/// anything more than a tap is too far.
class SalesHome extends StatefulWidget {
  const SalesHome({super.key});

  @override
  State<SalesHome> createState() => _SalesHomeState();
}

class _SalesHomeState extends State<SalesHome> {
  int _tab = 0;

  static const _screens = [
    DashboardScreen(),
    ShopsScreen(),
    OrdersScreen(),
    VanScreen(),
    MoreScreen(),
  ];

  @override
  Widget build(BuildContext context) => Scaffold(
        body: IndexedStack(index: _tab, children: _screens),
        bottomNavigationBar: DecoratedBox(
          decoration: const BoxDecoration(
            border: Border(top: BorderSide(color: AppColors.border)),
          ),
          child: NavigationBar(
            selectedIndex: _tab,
            onDestinationSelected: (index) => setState(() => _tab = index),
            destinations: const [
              NavigationDestination(
                icon: Icon(Icons.home_outlined),
                selectedIcon: Icon(Icons.home),
                label: 'Home',
              ),
              NavigationDestination(
                icon: Icon(Icons.storefront_outlined),
                selectedIcon: Icon(Icons.storefront),
                label: 'Shops',
              ),
              NavigationDestination(
                icon: Icon(Icons.assignment_outlined),
                selectedIcon: Icon(Icons.assignment),
                label: 'Orders',
              ),
              NavigationDestination(
                icon: Icon(Icons.local_shipping_outlined),
                selectedIcon: Icon(Icons.local_shipping),
                label: 'Van',
              ),
              NavigationDestination(
                icon: Icon(Icons.menu),
                selectedIcon: Icon(Icons.menu),
                label: 'More',
              ),
            ],
          ),
        ),
      );
}
