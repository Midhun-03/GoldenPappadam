import 'dart:async';

import 'package:connectivity_plus/connectivity_plus.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'data/local/database.dart';
import 'data/remote/api_client.dart';
import 'features/auth/login_screen.dart';
import 'features/home/home_screen.dart';
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
      theme: ThemeData(
        useMaterial3: true,
        colorScheme: ColorScheme.fromSeed(seedColor: const Color(0xFFC8871B)),
        // The salesperson is standing in a shop, often one-handed. Everything is big enough to
        // hit without looking twice.
        filledButtonTheme: FilledButtonThemeData(
          style: FilledButton.styleFrom(minimumSize: const Size.fromHeight(52)),
        ),
        inputDecorationTheme: const InputDecorationTheme(border: OutlineInputBorder()),
        listTileTheme: const ListTileThemeData(minVerticalPadding: 12),
      ),
      home: switch (session) {
        null => const Scaffold(body: Center(child: CircularProgressIndicator())),
        true => const HomeScreen(),
        false => const LoginScreen(),
      },
    );
  }
}
