import 'package:flutter/material.dart';

import '../../core/money.dart';
import '../../core/product_line_card.dart';
import '../../core/theme.dart';
import '../../data/local/database.dart';
import '../../data/shops_repository.dart';

/// One rate being entered: a product and what the shop will pay for it.
class RateLine {
  RateLine([this.product]);

  CachedProduct? product;
  final rate = TextEditingController();

  double get value => double.tryParse(rate.text) ?? 0;
}

/// The rate lines on a screen, so the screen can read them when it saves.
class RateLinesController extends ChangeNotifier {
  RateLinesController({CachedProduct? first}) : lines = [RateLine(first)];

  List<RateLine> lines;

  /// Every line with a product and a rate above zero.
  List<RateEntry> get entries => [
        for (final line in lines)
          if (line.product != null && line.value > 0)
            RateEntry(productId: line.product!.id, productName: line.product!.name, unitPrice: line.value),
      ];

  void add() {
    lines = [...lines, RateLine()];
    notifyListeners();
  }

  void remove(RateLine line) {
    lines = lines.where((other) => other != line).toList();
    line.rate.dispose();
    notifyListeners();
  }

  void changed() => notifyListeners();

  @override
  void dispose() {
    for (final line in lines) {
      line.rate.dispose();
    }
    super.dispose();
  }
}

/// Rates entered as product cards - the same card as a bill line, with a Rate field in place of
/// the quantity. A product can be on one line only. Each line says what the shop pays now, so the
/// salesperson sees what they are changing.
class RateLinesEditor extends StatelessWidget {
  const RateLinesEditor({
    required this.controller,
    required this.products,
    this.currentRates = const {},
    super.key,
  });

  final RateLinesController controller;
  final List<CachedProduct> products;

  /// What the shop pays now, by product id: its agreed rate or the standard price.
  final Map<String, double?> currentRates;

  @override
  Widget build(BuildContext context) => ListenableBuilder(
        listenable: controller,
        builder: (context, _) {
          final lines = controller.lines;

          List<CachedProduct> choicesFor(RateLine line) {
            final taken = {
              for (final other in lines)
                if (other != line && other.product != null) other.product!.id,
            };
            return products.where((p) => !taken.contains(p.id)).toList();
          }

          final canAdd = lines.every((line) => line.product != null) && lines.length < products.length;

          return Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              for (final (index, line) in lines.indexed)
                KeyedSubtree(
                  key: ObjectKey(line),
                  child: ProductLineCard(
                    name: 'rate',
                    index: index,
                    choices: choicesFor(line),
                    selected: line.product,
                    onSelected: (chosen) {
                      line.product = chosen;
                      controller.changed();
                    },
                    field: RateField(
                      controller: line.rate,
                      fieldKey: ValueKey('rate-value-$index'),
                      hint: _now(line)?.toStringAsFixed(2),
                      onChanged: (_) => controller.changed(),
                    ),
                    onRemove: lines.length == 1 ? null : () => controller.remove(line),
                    footer: _now(line) == null
                        ? null
                        : Text('Now ${money(_now(line)!)}', style: const TextStyle(color: AppColors.textMuted)),
                  ),
                ),
              AddItemButton(buttonKey: const ValueKey('rate-add-item'), onPressed: canAdd ? controller.add : null),
            ],
          );
        },
      );

  double? _now(RateLine line) => line.product == null ? null : currentRates[line.product!.id];
}
