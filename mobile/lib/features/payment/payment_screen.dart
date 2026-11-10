import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../app.dart';
import '../../core/money.dart';
import '../../core/theme.dart';
import '../../core/widgets.dart';
import '../../data/local/database.dart';

/// Money collected against what the shop already owes, with no delivery today.
///
/// Deliberately separate from the sale screen: the brief is explicit that a payment is not always
/// equal to today's bill, and the common case on the route is a shop settling last week's.
///
/// Reading order: who and how much they owe, the amount (the one thing typed), how it was paid,
/// and the button - which stays above the keyboard, so it is one tap from the last digit.
class PaymentScreen extends ConsumerStatefulWidget {
  const PaymentScreen({required this.shop, super.key});

  final CachedCustomer shop;

  @override
  ConsumerState<PaymentScreen> createState() => _PaymentScreenState();
}

class _PaymentScreenState extends ConsumerState<PaymentScreen> {
  final _amount = TextEditingController();
  final _reference = TextEditingController();
  String _method = 'Cash';
  bool _saving = false;
  String? _error;

  @override
  void dispose() {
    _amount.dispose();
    _reference.dispose();
    super.dispose();
  }

  double get _entered => double.tryParse(_amount.text.trim()) ?? 0;

  @override
  Widget build(BuildContext context) {
    final owed = widget.shop.balance;
    final entered = _entered;

    return Scaffold(
      appBar: AppBar(title: const Text('Receive Payment')),
      bottomNavigationBar: BottomActionBar(
        label: 'Collecting',
        amount: money(entered),
        actionLabel: 'Collect Payment',
        busy: _saving,
        onPressed: entered <= 0 ? null : _save,
      ),
      body: FadeIn(
        child: ListView(
          padding: const EdgeInsets.fromLTRB(16, 4, 16, 28),
          children: [
            _OwedCard(shopName: widget.shop.name, owed: owed, entered: entered),
            const SectionHeader('Amount received'),
            TextField(
              key: const ValueKey('payment-amount'),
              controller: _amount,
              autofocus: true,
              keyboardType: const TextInputType.numberWithOptions(decimal: true),
              style: const TextStyle(
                fontSize: 26,
                fontWeight: FontWeight.w700,
                letterSpacing: -0.3,
                fontFeatures: kTabularFigures,
              ),
              decoration: const InputDecoration(
                prefixText: '₹ ',
                hintText: '0',
                contentPadding: EdgeInsets.symmetric(horizontal: 16, vertical: 16),
              ),
              onChanged: (_) => setState(() {}),
            ),
            if (owed > 0) ...[
              const SizedBox(height: 10),
              // The whole balance in one tap, because "settling up" is the usual reason to
              // be here.
              OutlinedButton(
                onPressed: () => setState(() => _amount.text = owed.toStringAsFixed(2)),
                child: Text('The whole ${money(owed)}'),
              ),
            ],
            if (entered > owed && owed > 0) ...[
              const SizedBox(height: 12),
              InlineNotice(
                'That is more than the ${money(owed)} showing. It will be kept on account, '
                'and the office will see it against the next bill.',
              ),
            ],
            const SectionHeader('Received by'),
            // All three in view and one tap each, rather than a menu to open first.
            ChoiceRow<String>(
              key: const ValueKey('payment-method'),
              value: _method,
              onChanged: (method) => setState(() => _method = method),
              options: const [('Cash', 'Cash'), ('UPI', 'UPI'), ('Cheque', 'Cheque')],
            ),
            const SizedBox(height: 12),
            TextField(
              controller: _reference,
              decoration: const InputDecoration(
                labelText: 'Reference (optional)',
                hintText: 'Receipt or transaction number',
              ),
            ),
            if (_error != null) ...[
              const SizedBox(height: 12),
              InlineNotice(_error!, tone: Tone.danger, icon: Icons.error_outline),
            ],
          ],
        ),
      ),
    );
  }

  Future<void> _save() async {
    setState(() {
      _saving = true;
      _error = null;
    });

    try {
      await ref.read(salesRepositoryProvider).recordPayment(
            customerId: widget.shop.id,
            customerName: widget.shop.name,
            amount: _entered,
            method: _method,
            reference: _reference.text.trim().isEmpty ? null : _reference.text.trim(),
          );

      unawaitedSync(ref);

      if (!mounted) return;

      Navigator.of(context).pop();
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text('${money(_entered)} recorded from ${widget.shop.name}.')),
      );
    } catch (error) {
      if (mounted) setState(() => _error = 'Could not save that: $error');
    } finally {
      if (mounted) setState(() => _saving = false);
    }
  }
}

/// Who is paying and what they owe, with what will be left once the amount being typed is taken
/// off. The balance is the phone's last-known figure, as everywhere else in the app.
class _OwedCard extends StatelessWidget {
  const _OwedCard({required this.shopName, required this.owed, required this.entered});

  final String shopName;
  final double owed;
  final double entered;

  @override
  Widget build(BuildContext context) {
    final remaining = owed - entered;

    return AppCard(
      padding: EdgeInsets.zero,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Padding(
            padding: const EdgeInsets.fromLTRB(20, 18, 20, 18),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  shopName,
                  maxLines: 2,
                  overflow: TextOverflow.ellipsis,
                  style: Theme.of(context).textTheme.titleMedium,
                ),
                const SizedBox(height: 14),
                const Text('OUTSTANDING', style: kEyebrowStyle),
                const SizedBox(height: 4),
                if (owed > 0)
                  FittedBox(
                    fit: BoxFit.scaleDown,
                    alignment: Alignment.centerLeft,
                    child: Text(
                      money(owed),
                      style: const TextStyle(
                        fontSize: 32,
                        height: 1.15,
                        fontWeight: FontWeight.w700,
                        letterSpacing: -0.6,
                        color: AppColors.danger,
                        fontFeatures: kTabularFigures,
                      ),
                    ),
                  )
                else
                  const Text(
                    'Nothing outstanding',
                    style: TextStyle(
                      fontSize: 20,
                      fontWeight: FontWeight.w700,
                      color: AppColors.success,
                    ),
                  ),
              ],
            ),
          ),
          // Only once there is something to subtract, and only while it is not more than is
          // owed - that case has its own note under the amount.
          if (owed > 0 && entered > 0 && remaining >= 0) ...[
            const Divider(),
            Padding(
              padding: const EdgeInsets.symmetric(horizontal: 20, vertical: 13),
              child: Row(
                children: [
                  const Expanded(
                    child: Text(
                      'Left after this payment',
                      style: TextStyle(fontSize: 13, color: AppColors.textMuted),
                    ),
                  ),
                  AnimatedAmount(
                    money(remaining),
                    style: const TextStyle(
                      fontWeight: FontWeight.w700,
                      color: AppColors.charcoal,
                      fontFeatures: kTabularFigures,
                    ),
                  ),
                ],
              ),
            ),
          ],
        ],
      ),
    );
  }
}
