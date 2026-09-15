import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../app.dart';
import '../../data/local/database.dart';
import '../../sync/sync_engine.dart';

/// The bar that is always on screen. A salesperson should never have to wonder whether the office
/// has their morning's work: it says so without being asked.
class SyncBanner extends ConsumerWidget {
  const SyncBanner({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final pending = ref.watch(pendingCountProvider).value ?? 0;
    final status = ref.watch(syncStatusProvider).value;
    final scheme = Theme.of(context).colorScheme;

    final (Color background, Color foreground, IconData icon, String label) = switch (status) {
      SyncStatus(failed: > 0) => (
          scheme.errorContainer,
          scheme.onErrorContainer,
          Icons.error_outline,
          '${status.failed} could not be sent. Tap to see.'
        ),
      _ when pending > 0 => (
          scheme.secondaryContainer,
          scheme.onSecondaryContainer,
          Icons.cloud_upload_outlined,
          '$pending waiting to go up'
        ),
      SyncStatus(state: SyncState.offline) => (
          scheme.surfaceContainerHighest,
          scheme.onSurfaceVariant,
          Icons.cloud_off_outlined,
          'No connection. You can keep working.'
        ),
      _ => (scheme.surfaceContainerHighest, scheme.onSurfaceVariant, Icons.cloud_done_outlined,
          'Everything is with the office'),
    };

    return Material(
      color: background,
      child: InkWell(
        onTap: () => Navigator.of(context).push(
          MaterialPageRoute<void>(builder: (_) => const PendingScreen()),
        ),
        child: Padding(
          padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 10),
          child: Row(
            children: [
              Icon(icon, size: 18, color: foreground),
              const SizedBox(width: 10),
              Expanded(child: Text(label, style: TextStyle(color: foreground))),
              if (status?.state == SyncState.syncing)
                SizedBox(
                  height: 16,
                  width: 16,
                  child: CircularProgressIndicator(strokeWidth: 2, color: foreground),
                ),
            ],
          ),
        ),
      ),
    );
  }
}

/// What has not reached the office, and why. Never a dead end: everything here can be retried.
class PendingScreen extends ConsumerWidget {
  const PendingScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final db = ref.watch(databaseProvider);

    return Scaffold(
      appBar: AppBar(
        title: const Text('Waiting to go up'),
        actions: [
          IconButton(
            tooltip: 'Sync now',
            icon: const Icon(Icons.sync),
            onPressed: () => ref.read(syncProvider).syncNow(),
          ),
        ],
      ),
      body: StreamBuilder<List<OutboxEntry>>(
        stream: db.watchUnfinished(),
        builder: (context, snapshot) {
          final entries = snapshot.data ?? const <OutboxEntry>[];

          if (entries.isEmpty) {
            return const Center(child: Text('Everything is with the office.'));
          }

          return ListView.separated(
            itemCount: entries.length,
            separatorBuilder: (_, _) => const Divider(height: 1),
            itemBuilder: (context, index) {
              final entry = entries[index];
              final failed = entry.status == 'Failed';

              return ListTile(
                leading: Icon(
                  failed ? Icons.error_outline : Icons.schedule,
                  color: failed ? Theme.of(context).colorScheme.error : null,
                ),
                title: Text(entry.summary),
                subtitle: Text(failed
                    ? entry.lastError ?? 'The office refused this.'
                    : 'Saved here. It will go up on its own.'),
                trailing: failed
                    ? TextButton(
                        onPressed: () => db.retryNow(entry.clientRequestId),
                        child: const Text('Try again'),
                      )
                    : null,
              );
            },
          );
        },
      ),
    );
  }
}
