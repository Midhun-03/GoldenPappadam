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

    return Scaffold(
      appBar: AppBar(title: const Text('Receive Payment')),
      body: FadeIn(
        child: ListView(
          padding: const EdgeInsets.all(16),
          children: [
            AppCard(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  const Text('SHOP', style: kEyebrowStyle),
                  const SizedBox(height: 4),
                  Text(widget.shop.name, style: Theme.of(context).textTheme.titleMedium),
                  const Padding(
                    padding: EdgeInsets.symmetric(vertical: 12),
                    child: Divider(height: 1),
                  ),
                  const Text('OUTSTANDING', style: kEyebrowStyle),
                  const SizedBox(height: 4),
                  Text(
                    owed > 0 ? money(owed) : 'Nothing outstanding',
                    style: Theme.of(context).textTheme.headlineMedium?.copyWith(
                          color: owed > 0 ? AppColors.danger : AppColors.success,
                        ),
                  ),
                ],
              ),
            ),
            const SizedBox(height: 20),
            TextField(
              controller: _amount,
              autofocus: true,
              keyboardType: const TextInputType.numberWithOptions(decimal: true),
              decoration: const InputDecoration(labelText: 'Amount', prefixText: '₹ '),
              onChanged: (_) => setState(() {}),
            ),
            if (owed > 0) ...[
              const SizedBox(height: 10),
              // The whole balance in one tap, because "settling up" is the usual reason to be here.
              OutlinedButton(
                onPressed: () => setState(() => _amount.text = owed.toStringAsFixed(2)),
                child: Text('The whole ${money(owed)}'),
              ),
            ],
            const SizedBox(height: 20),
            DropdownButtonFormField<String>(
              key: const ValueKey('payment-method'),
              initialValue: _method,
              isExpanded: true,
              decoration: const InputDecoration(labelText: 'Payment method'),
              icon: const Icon(Icons.expand_more),
              items: const [
                DropdownMenuItem(value: 'Cash', child: Text('Cash')),
                DropdownMenuItem(value: 'UPI', child: Text('UPI')),
                DropdownMenuItem(value: 'Cheque', child: Text('Cheque')),
              ],
              onChanged: (value) => setState(() => _method = value ?? _method),
            ),
            const SizedBox(height: 12),
            TextField(
              controller: _reference,
              decoration: const InputDecoration(
                labelText: 'Reference (optional)',
                hintText: 'Receipt or transaction number',
              ),
            ),
            if (_entered > owed && owed > 0) ...[
              const SizedBox(height: 16),
              Text(
                'That is more than the ${money(owed)} showing. It will be kept on account, and the '
                'office will see it against the next bill.',
                style: const TextStyle(color: AppColors.warning),
              ),
            ],
            if (_error != null) ...[
              const SizedBox(height: 16),
              Text(_error!, style: const TextStyle(color: AppColors.danger)),
            ],
            const SizedBox(height: 24),
            FilledButton(
              onPressed: _saving || _entered <= 0 ? null : _save,
              child: const Text('Collect Payment'),
            ),
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
