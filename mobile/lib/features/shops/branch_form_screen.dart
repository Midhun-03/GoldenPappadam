import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../app.dart';
import '../../core/theme.dart';
import '../../core/widgets.dart';
import '../../data/local/database.dart';
import '../../data/shops_repository.dart';

/// One physical shop under a multi-branch customer - Coimbatore under Danya Supermarket - new or
/// edited. Never a new customer: a branch always belongs to [shop]. Closing a branch is the
/// office's call, so there is no such button here.
class BranchFormScreen extends ConsumerStatefulWidget {
  const BranchFormScreen({required this.shop, this.branch, super.key});

  final CachedCustomer shop;

  /// Null for a new branch.
  final CachedBranch? branch;

  @override
  ConsumerState<BranchFormScreen> createState() => _BranchFormScreenState();
}

class _BranchFormScreenState extends ConsumerState<BranchFormScreen> {
  final _form = GlobalKey<FormState>();
  late final _name = TextEditingController(text: widget.branch?.name ?? '');
  late final _location = TextEditingController(text: widget.branch?.location ?? '');
  late final _address = TextEditingController(text: widget.branch?.address ?? '');
  late final _phone = TextEditingController(text: widget.branch?.phone ?? '');
  late final _contact = TextEditingController(text: widget.branch?.contactPerson ?? '');
  bool _saving = false;

  @override
  void dispose() {
    for (final controller in [_name, _location, _address, _phone, _contact]) {
      controller.dispose();
    }
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    Widget field(String key, TextEditingController controller, String label,
            {int maxLength = 100, TextInputType? keyboard, String? Function(String?)? validator}) =>
        Padding(
          padding: const EdgeInsets.only(bottom: 12),
          child: TextFormField(
            key: ValueKey(key),
            controller: controller,
            keyboardType: keyboard,
            textCapitalization: keyboard == null ? TextCapitalization.words : TextCapitalization.none,
            maxLength: maxLength,
            decoration: InputDecoration(labelText: label, counterText: ''),
            validator: validator,
          ),
        );

    return Scaffold(
      appBar: AppBar(
        title: Text(widget.branch == null ? 'New branch' : 'Edit branch'),
      ),
      body: Form(
        key: _form,
        child: ListView(
          padding: const EdgeInsets.fromLTRB(12, 8, 12, 24),
          children: [
            Padding(
              padding: const EdgeInsets.fromLTRB(4, 4, 4, 10),
              child: Text('A branch of ${widget.shop.name}', style: kEyebrowStyle),
            ),
            AppCard(
              child: Column(
                children: [
                  field('branch-name', _name, 'Branch name *', maxLength: 150,
                      validator: (value) => (value ?? '').trim().isEmpty ? 'Enter the branch name' : null),
                  field('branch-location', _location, 'Place, e.g. Coimbatore'),
                  field('branch-address', _address, 'Address', maxLength: 300),
                  field('branch-phone', _phone, 'Phone', maxLength: 20, keyboard: TextInputType.phone),
                  field('branch-contact', _contact, 'Contact person'),
                ],
              ),
            ),
          ],
        ),
      ),
      bottomNavigationBar: SafeArea(
        child: Padding(
          padding: const EdgeInsets.fromLTRB(12, 8, 12, 12),
          child: FilledButton.icon(
            key: const ValueKey('save-branch'),
            onPressed: _saving ? null : _save,
            icon: const Icon(Icons.check, size: 18),
            label: Text(widget.branch == null ? 'Add branch' : 'Save changes'),
          ),
        ),
      ),
    );
  }

  Future<void> _save() async {
    if (!_form.currentState!.validate()) return;

    setState(() => _saving = true);

    try {
      await ref.read(shopsRepositoryProvider).saveBranch(
            widget.shop,
            BranchDetails(
              name: _name.text,
              location: _location.text,
              address: _address.text,
              phone: _phone.text,
              contactPerson: _contact.text,
            ),
            branchId: widget.branch?.id,
          );

      unawaitedSync(ref);
      if (mounted) Navigator.of(context).pop();
    } finally {
      if (mounted) setState(() => _saving = false);
    }
  }
}
