import 'package:flutter/material.dart';

import '../data/local/database.dart';
import 'searchable_dropdown.dart';
import 'theme.dart';
import 'widgets.dart';

/// One product line on a bill or a return: a product chosen from a dropdown you can type into, a
/// quantity, and whatever else that screen needs on the line - the price on a bill, the reason on a
/// return. One widget for both, so the two screens look and behave the same.
///
/// [name] prefixes the keys ("bill-line-0", "bill-qty-0", "bill-remove-0") so tests and screen
/// readers can tell lines, and screens, apart.
class ProductLineCard extends StatelessWidget {
  const ProductLineCard({
    required this.name,
    required this.index,
    required this.choices,
    required this.selected,
    required this.onSelected,
    required this.quantity,
    required this.onQuantityChanged,
    required this.onRemove,
    this.besideQuantity,
    this.belowQuantity,
    this.footer,
    super.key,
  });

  final String name;
  final int index;
  final List<CachedProduct> choices;
  final CachedProduct? selected;
  final ValueChanged<CachedProduct?> onSelected;
  final TextEditingController quantity;
  final ValueChanged<String> onQuantityChanged;

  /// Null when the line cannot be removed - the last one on the screen is changed, not deleted.
  final VoidCallback? onRemove;

  /// Next to the quantity, e.g. the read-only price.
  final Widget? besideQuantity;

  /// Under the quantity, e.g. the Expired / Damaged choice.
  final Widget? belowQuantity;

  /// Beside the delete button, e.g. the line total.
  final Widget? footer;

  static const _labelStyle = TextStyle(fontWeight: FontWeight.w600);

  @override
  Widget build(BuildContext context) => AppCard(
        key: ValueKey('$name-line-$index'),
        margin: const EdgeInsets.only(bottom: 10),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            const Text('Product', style: _labelStyle),
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
                Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    const Text('Quantity', style: _labelStyle),
                    const SizedBox(height: 6),
                    QuantityStepper(
                      controller: quantity,
                      fieldKey: ValueKey('$name-qty-$index'),
                      onChanged: onQuantityChanged,
                    ),
                  ],
                ),
                if (besideQuantity != null) ...[
                  const SizedBox(width: 12),
                  Expanded(child: besideQuantity!),
                ],
              ],
            ),
            if (belowQuantity != null) ...[
              const SizedBox(height: 12),
              belowQuantity!,
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

/// A labelled value that looks like a field but cannot be typed into - the price on a bill.
class ReadOnlyField extends StatelessWidget {
  const ReadOnlyField({required this.label, required this.value, super.key});

  final String label;
  final String value;

  @override
  Widget build(BuildContext context) => Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(label, style: const TextStyle(fontWeight: FontWeight.w600)),
          const SizedBox(height: 6),
          Container(
            height: 44,
            alignment: Alignment.centerRight,
            padding: const EdgeInsets.symmetric(horizontal: 12),
            decoration: BoxDecoration(
              color: AppColors.surfaceAlt,
              borderRadius: BorderRadius.circular(AppRadius.sm),
            ),
            child: Text(value, style: const TextStyle(color: AppColors.textMuted)),
          ),
        ],
      );
}
