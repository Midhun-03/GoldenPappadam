import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../app.dart';
import '../../core/theme.dart';
import '../../core/widgets.dart';
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

    final (Color background, Color foreground, IconData icon, String label) = switch (status) {
      SyncStatus(failed: > 0) => (
          AppColors.dangerSoft,
          AppColors.danger,
          Icons.error_outline,
          '${status.failed} could not be sent. Tap to see.'
        ),
      _ when pending > 0 => (
          AppColors.warningSoft,
          AppColors.warning,
          Icons.cloud_upload_outlined,
          '$pending waiting to go up'
        ),
      SyncStatus(state: SyncState.offline) => (
          AppColors.surfaceAlt,
          AppColors.textMuted,
          Icons.cloud_off_outlined,
          'No connection. You can keep working.'
        ),
      _ => (AppColors.surfaceAlt, AppColors.textMuted, Icons.cloud_done_outlined,
          'Everything is with the office'),
    };

    return Material(
      color: background,
      child: InkWell(
        onTap: () => Navigator.of(context).push(appRoute<void>((_) => const PendingScreen())),
        child: AnimatedSize(
          duration: AppMotion.normal,
          curve: AppMotion.curve,
          alignment: Alignment.topCenter,
          child: Padding(
            padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 10),
            child: Row(
              key: ValueKey(label),
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
      ),
    );
  }
}

/// The small "N PENDING" pill in the corner of Home, Shops, Orders and Van - one glance says
/// whether the office has today's work yet, without opening anything. Hidden once there is
/// nothing waiting, same as the banner: an empty state should not shout.
class PendingBadge extends ConsumerWidget {
  const PendingBadge({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final pending = ref.watch(pendingCountProvider).value ?? 0;
    if (pending == 0) return const SizedBox.shrink();

    return Padding(
      padding: const EdgeInsets.only(right: 12),
      child: InkWell(
        borderRadius: BorderRadius.circular(AppRadius.pill),
        onTap: () => Navigator.of(context).push(appRoute<void>((_) => const PendingScreen())),
        child: StatusPill('$pending PENDING', tone: Tone.warning, icon: Icons.circle),
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
            return const EmptyState(
              icon: Icons.cloud_done_outlined,
              title: 'Everything is with the office',
              message: 'Nothing here is waiting to go up.',
            );
          }

          return FadeIn(
            child: ListView.separated(
              itemCount: entries.length,
              separatorBuilder: (_, _) => const Divider(height: 1),
              itemBuilder: (context, index) {
                final entry = entries[index];
                final failed = entry.status == 'Failed';

                return ListTile(
                  leading: Icon(
                    failed ? Icons.error_outline : Icons.schedule,
                    color: failed ? AppColors.danger : AppColors.textMuted,
                  ),
                  title: Text(entry.summary, style: const TextStyle(fontWeight: FontWeight.w600)),
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
            ),
          );
        },
      ),
    );
  }
}
