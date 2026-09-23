import 'package:flutter/material.dart';
import 'package:flutter/services.dart';

import 'theme.dart';

/// A short, lightweight page transition used everywhere a screen pushes another one, so the app
/// feels like one coherent thing rather than mismatched platform defaults. Fade plus a small
/// upward slide - nothing that costs a low-end phone anything a normal push would not.
Route<T> appRoute<T>(WidgetBuilder builder) => PageRouteBuilder<T>(
      pageBuilder: (context, animation, secondaryAnimation) => builder(context),
      transitionsBuilder: (context, animation, secondaryAnimation, child) {
        final curved = CurvedAnimation(parent: animation, curve: AppMotion.curve);

        return FadeTransition(
          opacity: curved,
          child: SlideTransition(
            position: Tween<Offset>(begin: const Offset(0, 0.04), end: Offset.zero).animate(curved),
            child: child,
          ),
        );
      },
      transitionDuration: AppMotion.normal,
    );

/// The one card shape used across the app: a flat surface with a hairline border instead of a
/// shadow, which is what keeps a screen full of cards from looking busy.
class AppCard extends StatelessWidget {
  const AppCard({
    required this.child,
    this.padding = const EdgeInsets.all(AppSpacing.lg),
    this.margin,
    this.onTap,
    this.color,
    super.key,
  });

  final Widget child;
  final EdgeInsetsGeometry padding;
  final EdgeInsetsGeometry? margin;
  final VoidCallback? onTap;
  final Color? color;

  @override
  Widget build(BuildContext context) {
    final radius = BorderRadius.circular(AppRadius.md);

    final card = Material(
      color: color ?? AppColors.surface,
      borderRadius: radius,
      child: InkWell(
        borderRadius: radius,
        onTap: onTap,
        child: Container(
          padding: padding,
          decoration: BoxDecoration(borderRadius: radius, border: Border.all(color: AppColors.border)),
          child: child,
        ),
      ),
    );

    return margin == null ? card : Padding(padding: margin!, child: card);
  }
}

/// A small caps, letter-spaced label above a group of related content - "TODAY SO FAR", "VAN
/// STOCK", "PAYMENT HISTORY". One style for every section eyebrow in the app.
class SectionHeader extends StatelessWidget {
  const SectionHeader(this.title, {this.trailing, this.padding, super.key});

  final String title;
  final Widget? trailing;
  final EdgeInsetsGeometry? padding;

  @override
  Widget build(BuildContext context) => Padding(
        padding: padding ?? const EdgeInsets.fromLTRB(4, 20, 4, 8),
        child: Row(
          children: [
            Expanded(child: Text(title.toUpperCase(), style: kEyebrowStyle)),
            ?trailing,
          ],
        ),
      );
}

/// A numbered step eyebrow for a page built as a sequence - "1 · SELECT SHOP", "2 · ADD
/// PRODUCTS" - with room for a short caption on the right ("PRICE IS READ-ONLY").
class NumberedStepHeader extends StatelessWidget {
  const NumberedStepHeader(this.step, this.title, {this.caption, super.key});

  final int step;
  final String title;
  final String? caption;

  @override
  Widget build(BuildContext context) => Padding(
        padding: const EdgeInsets.fromLTRB(4, 20, 4, 8),
        child: Row(
          children: [
            Expanded(child: Text('$step · ${title.toUpperCase()}', style: kEyebrowStyle)),
            if (caption != null) Text(caption!.toUpperCase(), style: kEyebrowStyle),
          ],
        ),
      );
}

enum Tone { neutral, primary, success, warning, danger }

(Color, Color) _toneColors(Tone tone) => switch (tone) {
      Tone.primary => (AppColors.goldSoft, AppColors.goldDark),
      Tone.success => (AppColors.successSoft, AppColors.success),
      Tone.warning => (AppColors.warningSoft, AppColors.warning),
      Tone.danger => (AppColors.dangerSoft, AppColors.danger),
      Tone.neutral => (AppColors.surfaceAlt, AppColors.textMuted),
    };

/// A small rounded pill for a status word - "Failed", "Pending", "Owes ₹450" - so meaning is
/// carried by shape and colour together rather than colour alone.
class StatusPill extends StatelessWidget {
  const StatusPill(this.label, {this.tone = Tone.neutral, this.icon, super.key});

  final String label;
  final Tone tone;
  final IconData? icon;

  @override
  Widget build(BuildContext context) {
    final (background, foreground) = _toneColors(tone);

    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 5),
      decoration: BoxDecoration(color: background, borderRadius: BorderRadius.circular(AppRadius.pill)),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          if (icon != null) ...[
            Icon(icon, size: 13, color: foreground),
            const SizedBox(width: 4),
          ],
          Text(label, style: TextStyle(color: foreground, fontSize: 12, fontWeight: FontWeight.w600)),
        ],
      ),
    );
  }
}

/// One figure on the home screen's grid: an icon, a label, a value, and an optional note under it.
class StatTile extends StatelessWidget {
  const StatTile({
    required this.icon,
    required this.label,
    required this.value,
    this.note,
    this.tone = Tone.neutral,
    this.onTap,
    super.key,
  });

  final IconData icon;
  final String label;
  final String value;
  final String? note;
  final Tone tone;
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) {
    final (background, foreground) = _toneColors(tone);

    return AppCard(
      onTap: onTap,
      padding: const EdgeInsets.all(14),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        mainAxisSize: MainAxisSize.min,
        children: [
          Row(
            children: [
              Expanded(
                child: Text(label,
                    style: const TextStyle(
                        fontSize: 12, color: AppColors.textMuted, fontWeight: FontWeight.w500)),
              ),
              Container(
                padding: const EdgeInsets.all(5),
                decoration:
                    BoxDecoration(color: background, borderRadius: BorderRadius.circular(AppRadius.sm)),
                child: Icon(icon, size: 14, color: foreground),
              ),
            ],
          ),
          const SizedBox(height: 10),
          Text(value,
              maxLines: 1,
              overflow: TextOverflow.ellipsis,
              style: const TextStyle(fontSize: 19, fontWeight: FontWeight.w700, color: AppColors.charcoal)),
          if (note != null) ...[
            const SizedBox(height: 3),
            Text(note!.toUpperCase(), maxLines: 1, overflow: TextOverflow.ellipsis, style: kEyebrowStyle),
          ],
        ],
      ),
    );
  }
}

/// The centred icon-title-message block shown wherever a list has nothing in it yet.
class EmptyState extends StatelessWidget {
  const EmptyState({required this.icon, required this.title, required this.message, super.key});

  final IconData icon;
  final String title;
  final String message;

  @override
  Widget build(BuildContext context) => Center(
        child: Padding(
          padding: const EdgeInsets.all(32),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              Container(
                padding: const EdgeInsets.all(18),
                decoration: const BoxDecoration(color: AppColors.surfaceAlt, shape: BoxShape.circle),
                child: Icon(icon, size: 30, color: AppColors.textMuted),
              ),
              const SizedBox(height: 16),
              Text(title, style: Theme.of(context).textTheme.titleMedium, textAlign: TextAlign.center),
              const SizedBox(height: 6),
              Text(message,
                  textAlign: TextAlign.center,
                  style: const TextStyle(color: AppColors.textMuted, height: 1.4)),
            ],
          ),
        ),
      );
}

/// Wraps money (or any short label) so a change in value fades and slides into place instead of
/// snapping - used on the bill's line totals and the grand total.
class AnimatedAmount extends StatelessWidget {
  const AnimatedAmount(this.text, {this.style, super.key});

  final String text;
  final TextStyle? style;

  @override
  Widget build(BuildContext context) => AnimatedSwitcher(
        duration: AppMotion.fast,
        transitionBuilder: (child, animation) => FadeTransition(
          opacity: animation,
          child: SlideTransition(
            position: Tween<Offset>(begin: const Offset(0, 0.25), end: Offset.zero).animate(animation),
            child: child,
          ),
        ),
        child: Text(text, key: ValueKey(text), style: style),
      );
}

/// A subtle press-down feel for a card-sized tap target, without a full custom button. Used for
/// the one primary action a screen wants to feel tactile - "New bill" on Home.
class Pressable extends StatefulWidget {
  const Pressable({required this.child, required this.onTap, super.key});

  final Widget child;
  final VoidCallback onTap;

  @override
  State<Pressable> createState() => _PressableState();
}

class _PressableState extends State<Pressable> {
  double _scale = 1;

  void _set(double value) => setState(() => _scale = value);

  @override
  Widget build(BuildContext context) => GestureDetector(
        onTapDown: (_) => _set(0.97),
        onTapCancel: () => _set(1),
        onTapUp: (_) => _set(1),
        onTap: widget.onTap,
        child: AnimatedScale(
          scale: _scale,
          duration: AppMotion.fast,
          curve: Curves.easeOut,
          child: widget.child,
        ),
      );
}

/// One product with a quantity stepper beside it - the row a stock request and a van load sheet
/// both build a whole screen out of. No price here on purpose: neither screen is selling
/// anything, only counting it.
///
/// [controller] is owned by the caller (one per product, created lazily and disposed with the
/// screen) - a stepper's +/- buttons write to it directly, so it cannot be recreated on every
/// rebuild the way a bare [TextField] can.
class ProductQuantityRow extends StatelessWidget {
  const ProductQuantityRow({
    required this.name,
    required this.unit,
    required this.controller,
    required this.onChanged,
    super.key,
  });

  final String name;
  final String unit;
  final ValueChanged<String> onChanged;
  final TextEditingController controller;

  @override
  Widget build(BuildContext context) => Padding(
        padding: const EdgeInsets.symmetric(horizontal: 4, vertical: 8),
        child: Row(
          children: [
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(name, style: const TextStyle(fontWeight: FontWeight.w600)),
                  Text(unit, style: const TextStyle(fontSize: 12, color: AppColors.textMuted)),
                ],
              ),
            ),
            const SizedBox(width: 8),
            QuantityStepper(controller: controller, onChanged: onChanged),
          ],
        ),
      );
}

/// A "− [value] +" quantity control. The number in the middle is a real, small [TextField] bound
/// to [controller] - tapping it still lets you type a large quantity directly - so this reads as
/// a stepper but keeps every existing search/enterText path working unchanged.
class QuantityStepper extends StatelessWidget {
  const QuantityStepper({
    required this.controller,
    required this.onChanged,
    this.fieldKey,
    this.min = 0,
    this.step = 1,
    super.key,
  });

  final TextEditingController controller;
  final ValueChanged<String> onChanged;
  final Key? fieldKey;
  final double min;
  final double step;

  double get _value => double.tryParse(controller.text) ?? 0;

  void _nudge(double delta) {
    final next = _value + delta;
    final clamped = next < min ? min : next;
    final text = clamped == clamped.roundToDouble() ? clamped.toInt().toString() : '$clamped';
    controller.text = clamped <= 0 ? '' : text;
    onChanged(controller.text);
  }

  @override
  Widget build(BuildContext context) => Container(
        height: 40,
        padding: const EdgeInsets.symmetric(horizontal: 2),
        decoration: BoxDecoration(
          color: AppColors.surfaceAlt,
          borderRadius: BorderRadius.circular(AppRadius.sm),
        ),
        child: Row(
          mainAxisSize: MainAxisSize.min,
          children: [
            _StepButton(icon: Icons.remove, onTap: () => _nudge(-step)),
            SizedBox(
              width: 40,
              child: TextField(
                key: fieldKey,
                controller: controller,
                textAlign: TextAlign.center,
                style: const TextStyle(fontWeight: FontWeight.w700),
                keyboardType: const TextInputType.numberWithOptions(decimal: true),
                inputFormatters: [FilteringTextInputFormatter.allow(RegExp(r'[0-9.]'))],
                decoration: const InputDecoration(
                  isDense: true,
                  hintText: '0',
                  filled: false,
                  border: InputBorder.none,
                  contentPadding: EdgeInsets.zero,
                ),
                onChanged: onChanged,
                onTapOutside: (_) => FocusManager.instance.primaryFocus?.unfocus(),
              ),
            ),
            _StepButton(icon: Icons.add, onTap: () => _nudge(step), emphasized: true),
          ],
        ),
      );
}

class _StepButton extends StatelessWidget {
  const _StepButton({required this.icon, required this.onTap, this.emphasized = false});

  final IconData icon;
  final VoidCallback onTap;
  final bool emphasized;

  @override
  Widget build(BuildContext context) => Material(
        color: emphasized ? AppColors.charcoal : Colors.transparent,
        borderRadius: BorderRadius.circular(AppRadius.sm),
        child: InkWell(
          borderRadius: BorderRadius.circular(AppRadius.sm),
          onTap: onTap,
          child: SizedBox(
            width: 32,
            height: 36,
            child: Icon(icon, size: 16, color: emphasized ? Colors.white : AppColors.charcoal),
          ),
        ),
      );
}

/// A row of equal-width choices - Credit / Paid / Part Paid - with the selected one picked out
/// by a gold border and tint rather than a solid fill, so several can sit side by side without
/// competing with the page's one primary action.
class ChoiceRow<T> extends StatelessWidget {
  const ChoiceRow({required this.options, required this.value, required this.onChanged, super.key});

  final List<(T, String)> options;
  final T value;
  final ValueChanged<T> onChanged;

  @override
  Widget build(BuildContext context) => Row(
        children: [
          for (final (index, option) in options.indexed) ...[
            if (index > 0) const SizedBox(width: 8),
            Expanded(
              child: _ChoiceButton(
                label: option.$2,
                selected: option.$1 == value,
                onTap: () => onChanged(option.$1),
              ),
            ),
          ],
        ],
      );
}

class _ChoiceButton extends StatelessWidget {
  const _ChoiceButton({required this.label, required this.selected, required this.onTap});

  final String label;
  final bool selected;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) => Material(
        color: selected ? AppColors.goldSoft : AppColors.surface,
        borderRadius: BorderRadius.circular(AppRadius.md),
        child: InkWell(
          borderRadius: BorderRadius.circular(AppRadius.md),
          onTap: onTap,
          child: AnimatedContainer(
            duration: AppMotion.fast,
            height: 48,
            alignment: Alignment.center,
            decoration: BoxDecoration(
              borderRadius: BorderRadius.circular(AppRadius.md),
              border: Border.all(color: selected ? AppColors.gold : AppColors.border, width: 1.4),
            ),
            child: Text(
              label,
              style: TextStyle(
                fontWeight: FontWeight.w700,
                color: selected ? AppColors.goldDark : AppColors.charcoal,
              ),
            ),
          ),
        ),
      );
}

/// A plain fade-and-rise entrance for the first thing a screen shows, so content does not just
/// pop into place. Deliberately not used per list row - one entrance per screen is enough, and
/// costs nothing extra on a long list.
class FadeIn extends StatelessWidget {
  const FadeIn({required this.child, super.key});

  final Widget child;

  @override
  Widget build(BuildContext context) => TweenAnimationBuilder<double>(
        tween: Tween(begin: 0, end: 1),
        duration: AppMotion.normal,
        curve: AppMotion.curve,
        builder: (context, value, child) => Opacity(
          opacity: value,
          child: Transform.translate(offset: Offset(0, (1 - value) * 8), child: child),
        ),
        child: child,
      );
}
