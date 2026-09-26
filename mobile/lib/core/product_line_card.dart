import 'package:flutter/material.dart';

import '../data/local/database.dart';
import 'searchable_dropdown.dart';
import 'theme.dart';
import 'widgets.dart';

/// One product line: a product chosen from a dropdown you can type into, the line's own field, and
/// a delete button. The one way products are entered anywhere in this app - bills, returns, rates -
/// so every screen that takes products looks and behaves the same. Add a new kind of field here
/// rather than building a different list for a new screen.
///
/// [field] is what the line records about the product: [QuantityField] on a bill or a return,
/// [RateField] when setting what a shop pays. [name] prefixes the keys ("bill-line-0",
/// "bill-remove-0") so tests and screen readers can tell lines, and screens, apart.
class ProductLineCard extends StatelessWidget {
  const ProductLineCard({
    required this.name,
    required this.index,
    required this.choices,
    required this.selected,
    required this.onSelected,
    required this.field,
    required this.onRemove,
    this.besideField,
    this.below,
    this.footer,
    super.key,
  });

  final String name;
  final int index;
  final List<CachedProduct> choices;
  final CachedProduct? selected;
  final ValueChanged<CachedProduct?> onSelected;

  /// The line's input: a quantity, a rate.
  final Widget field;

  /// Null when the line cannot be removed - the last one on the screen is changed, not deleted.
  final VoidCallback? onRemove;

  /// Next to [field], e.g. the read-only price on a bill.
  final Widget? besideField;

  /// Under [field], e.g. the Expired / Damaged choice on a return.
  final Widget? below;

  /// Beside the delete button, e.g. the line total.
  final Widget? footer;

  @override
  Widget build(BuildContext context) => AppCard(
        key: ValueKey('$name-line-$index'),
        margin: const EdgeInsets.only(bottom: 10),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            const Text('Product', style: LabeledField.labelStyle),
            const SizedBox(height: 6),
            SearchableDropdown<CachedProduct>(
              hintText: 'Choose a product',
              selected: selected,
              onSelected: onSelected,
              entries: [
                for (final choice in choices) DropdownMenuEntry(value: choice, label: choice.name),
              ],
            ),
            const SizedBox(height: 12),
            Row(
              crossAxisAlignment: CrossAxisAlignment.end,
              children: [
                if (besideField == null) Expanded(child: field) else field,
                if (besideField != null) ...[
                  const SizedBox(width: 12),
                  Expanded(child: besideField!),
                ],
              ],
            ),
            if (below != null) ...[
              const SizedBox(height: 12),
              below!,
            ],
            const SizedBox(height: 4),
            Row(
              children: [
                Expanded(child: footer ?? const SizedBox.shrink()),
                IconButton(
                  key: ValueKey('$name-remove-$index'),
                  tooltip: 'Remove item',
                  icon: const Icon(Icons.delete_outline),
                  onPressed: onRemove,
                ),
              ],
            ),
          ],
        ),
      );
}

/// The "Add item" button under a list of [ProductLineCard]s.
class AddItemButton extends StatelessWidget {
  const AddItemButton({required this.onPressed, this.buttonKey, super.key});

  /// Null while the last line is still empty, or there is nothing left to add.
  final VoidCallback? onPressed;
  final Key? buttonKey;

  @override
  Widget build(BuildContext context) => Align(
        alignment: Alignment.centerLeft,
        child: OutlinedButton.icon(
          key: buttonKey,
          onPressed: onPressed,
          icon: const Icon(Icons.add, size: 18),
          label: const Text('Add item'),
        ),
      );
}

/// A small label over a field, the same on every line.
class LabeledField extends StatelessWidget {
  const LabeledField({required this.label, required this.child, super.key});

  static const labelStyle = TextStyle(fontWeight: FontWeight.w600);

  final String label;
  final Widget child;

  @override
  Widget build(BuildContext context) => Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(label, style: labelStyle),
          const SizedBox(height: 6),
          child,
        ],
      );
}

/// How many: the − / + stepper, which can still be typed into.
class QuantityField extends StatelessWidget {
  const QuantityField({required this.controller, required this.onChanged, this.fieldKey, super.key});

  final TextEditingController controller;
  final ValueChanged<String> onChanged;
  final Key? fieldKey;

  @override
  Widget build(BuildContext context) => LabeledField(
        label: 'Quantity',
        child: QuantityStepper(controller: controller, fieldKey: fieldKey, onChanged: onChanged),
      );
}

/// What the shop pays for the product, typed in rupees. Only on rate screens: a bill's price is
/// never typed on the phone.
class RateField extends StatelessWidget {
  const RateField({required this.controller, required this.onChanged, this.hint, this.fieldKey, super.key});

  final TextEditingController controller;
  final ValueChanged<String> onChanged;

  /// What it is now, shown faintly until something is typed.
  final String? hint;
  final Key? fieldKey;

  @override
  Widget build(BuildContext context) => LabeledField(
        label: 'Rate',
        child: TextField(
          key: fieldKey,
          controller: controller,
          onChanged: onChanged,
          keyboardType: const TextInputType.numberWithOptions(decimal: true),
          textAlign: TextAlign.right,
          decoration: InputDecoration(prefixText: '₹ ', hintText: hint, isDense: true),
        ),
      );
}

/// A labelled value that looks like a field but cannot be typed into - the price on a bill.
class ReadOnlyField extends StatelessWidget {
  const ReadOnlyField({required this.label, required this.value, super.key});

  final String label;
  final String value;

  @override
  Widget build(BuildContext context) => LabeledField(
        label: label,
        child: Container(
          height: 44,
          alignment: Alignment.centerRight,
          padding: const EdgeInsets.symmetric(horizontal: 12),
          decoration: BoxDecoration(
            color: AppColors.surfaceAlt,
            borderRadius: BorderRadius.circular(AppRadius.sm),
          ),
          child: Text(value, style: const TextStyle(color: AppColors.textMuted)),
        ),
      );
}
