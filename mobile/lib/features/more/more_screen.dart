import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../app.dart';
import '../../core/money.dart';
import '../../core/theme.dart';
import '../../core/widgets.dart';
import '../../sync/sync_engine.dart';
import '../sync/sync_views.dart';

/// Everything that is not a daily action: who this phone is signed in as, whether the office has
/// today's work, and the way out. Deliberately the smallest screen in the app - a salesperson
/// opens it rarely, so it earns its place in the tab bar only by being where sign-out lives.
class MoreScreen extends ConsumerWidget {
  const MoreScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final status = ref.watch(syncStatusProvider).value;
    final pending = ref.watch(pendingCountProvider).value ?? 0;

    return Scaffold(
      appBar: AppBar(title: const Text('More')),
      body: ListView(
        padding: const EdgeInsets.all(12),
        children: [
          const AppCard(
            child: Row(
              children: [
                CircleAvatar(
                  radius: 22,
                  backgroundColor: AppColors.charcoal,
                  child: Icon(Icons.person_outline, color: Colors.white),
                ),
                SizedBox(width: 14),
                Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text('Salesperson',
                        style: TextStyle(fontWeight: FontWeight.w700, fontSize: 16)),
                    Text('Field sales', style: TextStyle(color: AppColors.textMuted, fontSize: 13)),
                  ],
                ),
              ],
            ),
          ),
          const SectionHeader('Sync status'),
          _SyncStatusCard(status: status, pending: pending),
          const SizedBox(height: 24),
          OutlinedButton.icon(
            onPressed: () => _confirmSignOut(context, ref),
            icon: const Icon(Icons.logout, size: 18),
            label: const Text('Sign out'),
          ),
        ],
      ),
    );
  }

  Future<void> _confirmSignOut(BuildContext context, WidgetRef ref) async {
    final pending = ref.read(pendingCountProvider).value ?? 0;

    final confirmed = await showDialog<bool>(
      context: context,
      builder: (context) => AlertDialog(
        title: const Text('Sign out?'),
        content: Text(
          pending == 0
              ? 'Everything is with the office. You can sign back in any time.'
              : 'There is $pending still waiting to sync. It stays saved on this phone and goes '
                  'up the next time you sign in with a connection.',
        ),
        actions: [
          TextButton(onPressed: () => Navigator.of(context).pop(false), child: const Text('Cancel')),
          FilledButton(
              onPressed: () => Navigator.of(context).pop(true), child: const Text('Sign out')),
        ],
      ),
    );

    if (confirmed != true || !context.mounted) return;

    ref.read(syncProvider).stop();
    await ref.read(databaseProvider).clearForSignOut();
    await ref.read(apiProvider).signOut();
    ref.read(sessionProvider.notifier).set(signedIn: false);
  }
}

class _SyncStatusCard extends StatelessWidget {
  const _SyncStatusCard({required this.status, required this.pending});

  final SyncStatus? status;
  final int pending;

  @override
  Widget build(BuildContext context) {
    final (IconData icon, Tone tone, String label, String note) = switch (status) {
      SyncStatus(failed: > 0) => (
          Icons.error_outline,
          Tone.danger,
          '${status!.failed} could not be sent',
          'Open it to see why, and to try again.'
        ),
      _ when pending > 0 => (
          Icons.cloud_upload_outlined,
          Tone.warning,
          '$pending waiting to sync',
          'Saved safely on this phone.'
        ),
      SyncStatus(state: SyncState.offline) => (
          Icons.cloud_off_outlined,
          Tone.neutral,
          'No connection',
          'You can keep working - it goes up once you are back in signal.'
        ),
      _ => (
          Icons.cloud_done_outlined,
          Tone.success,
          'All synced',
          'The office has everything from this phone.'
        ),
    };

    final (background, foreground) = switch (tone) {
      Tone.danger => (AppColors.dangerSoft, AppColors.danger),
      Tone.warning => (AppColors.warningSoft, AppColors.warning),
      Tone.success => (AppColors.successSoft, AppColors.success),
      _ => (AppColors.surfaceAlt, AppColors.textMuted),
    };

    return AppCard(
      color: background,
      onTap: () => Navigator.of(context).push(appRoute<void>((_) => const PendingScreen())),
      child: Row(
        children: [
          Icon(icon, color: foreground),
          const SizedBox(width: 12),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(label, style: TextStyle(fontWeight: FontWeight.w700, color: foreground)),
                Text(note, style: const TextStyle(fontSize: 12.5, color: AppColors.textMuted)),
                if (status?.lastSyncedAt != null) ...[
                  const SizedBox(height: 2),
                  Text('Last synced ${howLongAgo(status!.lastSyncedAt)}',
                      style: const TextStyle(fontSize: 11.5, color: AppColors.textMuted)),
                ],
              ],
            ),
          ),
          Icon(Icons.chevron_right, color: foreground),
        ],
      ),
    );
  }
}
