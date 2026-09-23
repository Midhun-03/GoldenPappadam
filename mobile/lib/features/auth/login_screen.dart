import 'dart:io';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../app.dart';
import '../../core/theme.dart';
import '../../data/remote/api_client.dart';

class LoginScreen extends ConsumerStatefulWidget {
  const LoginScreen({super.key});

  @override
  ConsumerState<LoginScreen> createState() => _LoginScreenState();
}

class _LoginScreenState extends ConsumerState<LoginScreen> {
  final _email = TextEditingController();
  final _password = TextEditingController();
  bool _busy = false;
  String? _error;

  @override
  void dispose() {
    _email.dispose();
    _password.dispose();
    super.dispose();
  }

  Future<void> _signIn() async {
    setState(() {
      _busy = true;
      _error = null;
    });

    try {
      await ref.read(apiProvider).signIn(_email.text.trim(), _password.text);

      // Tell the office which handset this is, then fetch everything needed to work offline.
      // Both need signal, which is fine: signing in needed signal anyway.
      final sync = ref.read(syncProvider);
      await sync.registerDevice(name: _deviceName(), platform: Platform.operatingSystem);
      await sync.syncNow();

      if (!mounted) return;

      ref.read(sessionProvider.notifier).set(signedIn: true);
      ref.read(connectivityProvider);
      sync.start();
    } on ApiException catch (error) {
      setState(() => _error = error.message);
    } on OfflineException catch (error) {
      setState(() => _error = '${error.message} Signing in for the first time needs a connection.');
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  static String _deviceName() => Platform.localHostname.isEmpty ? 'Phone' : Platform.localHostname;

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      body: SafeArea(
        child: Center(
          child: SingleChildScrollView(
            padding: const EdgeInsets.all(24),
            child: Column(
              mainAxisSize: MainAxisSize.min,
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                Container(
                  width: 72,
                  height: 72,
                  alignment: Alignment.center,
                  decoration: const BoxDecoration(color: AppColors.goldSoft, shape: BoxShape.circle),
                  child: const Icon(Icons.storefront, color: AppColors.goldDark, size: 34),
                ),
                const SizedBox(height: 20),
                Text('Golden Pappadam',
                    style: Theme.of(context).textTheme.headlineSmall,
                    textAlign: TextAlign.center),
                const SizedBox(height: 4),
                const Text('Sales',
                    style: TextStyle(color: AppColors.textMuted), textAlign: TextAlign.center),
                const SizedBox(height: 36),
                TextField(
                  controller: _email,
                  decoration: const InputDecoration(
                    labelText: 'Email',
                    prefixIcon: Icon(Icons.mail_outline),
                  ),
                  keyboardType: TextInputType.emailAddress,
                  autocorrect: false,
                  textInputAction: TextInputAction.next,
                ),
                const SizedBox(height: 12),
                TextField(
                  controller: _password,
                  decoration: const InputDecoration(
                    labelText: 'Password',
                    prefixIcon: Icon(Icons.lock_outline),
                  ),
                  obscureText: true,
                  onSubmitted: (_) => _busy ? null : _signIn(),
                ),
                if (_error != null) ...[
                  const SizedBox(height: 16),
                  Text(_error!, style: const TextStyle(color: AppColors.danger)),
                ],
                const SizedBox(height: 24),
                FilledButton(
                  onPressed: _busy ? null : _signIn,
                  child: _busy
                      ? const SizedBox(
                          height: 20,
                          width: 20,
                          child: CircularProgressIndicator(strokeWidth: 2, color: Colors.white))
                      : const Text('Sign in'),
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}
