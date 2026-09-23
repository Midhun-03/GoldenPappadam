import 'package:flutter/material.dart';

/// The palette behind every screen: a warm neutral background, charcoal text, and the Golden
/// Pappadam gold as the one accent. Semantic tones (success/warning/danger) are used sparingly -
/// only where a colour carries real meaning, such as an outstanding balance or a failed sync.
abstract final class AppColors {
  static const gold = Color(0xFFE3A62E);
  static const goldDark = Color(0xFFAD7714);
  static const goldSoft = Color(0xFFFCEFD3);

  static const background = Color(0xFFF8F7F4);
  static const surface = Colors.white;
  static const surfaceAlt = Color(0xFFF1EFE9);
  static const border = Color(0xFFE6E2D9);

  static const charcoal = Color(0xFF2A2620);
  static const textMuted = Color(0xFF74695A);

  static const success = Color(0xFF1E7A46);
  static const successSoft = Color(0xFFE4F3EA);

  static const warning = Color(0xFFAD7A12);
  static const warningSoft = Color(0xFFFBF0DC);

  static const danger = Color(0xFFC1392B);
  static const dangerSoft = Color(0xFFFBEAE8);
}

abstract final class AppSpacing {
  static const xs = 4.0;
  static const sm = 8.0;
  static const md = 12.0;
  static const lg = 16.0;
  static const xl = 20.0;
  static const xxl = 28.0;
}

abstract final class AppRadius {
  static const sm = 8.0;
  static const md = 12.0;
  static const lg = 16.0;
  static const pill = 999.0;
}

/// Every animation in the app shares one duration and curve, so nothing feels out of step with
/// anything else. Kept short and simple on purpose - a low-end phone has to run it too.
abstract final class AppMotion {
  static const fast = Duration(milliseconds: 150);
  static const normal = Duration(milliseconds: 200);
  static const curve = Curves.easeOutCubic;
}

abstract final class AppTheme {
  static ThemeData light() {
    final scheme = ColorScheme.fromSeed(
      seedColor: AppColors.gold,
      brightness: Brightness.light,
      error: AppColors.danger,
    ).copyWith(
      primary: AppColors.gold,
      surface: AppColors.surface,
      primaryContainer: AppColors.goldSoft,
      onPrimaryContainer: AppColors.goldDark,
      errorContainer: AppColors.dangerSoft,
      onErrorContainer: AppColors.danger,
      secondaryContainer: AppColors.surfaceAlt,
      onSecondaryContainer: AppColors.charcoal,
      surfaceContainerHighest: AppColors.surfaceAlt,
      onSurfaceVariant: AppColors.textMuted,
    );

    final base = ThemeData(useMaterial3: true, colorScheme: scheme);

    return base.copyWith(
      scaffoldBackgroundColor: AppColors.background,
      splashFactory: InkSparkle.splashFactory,
      textTheme: base.textTheme
          .apply(bodyColor: AppColors.charcoal, displayColor: AppColors.charcoal)
          .copyWith(
            titleLarge: base.textTheme.titleLarge
                ?.copyWith(fontWeight: FontWeight.w700, color: AppColors.charcoal),
            titleMedium: base.textTheme.titleMedium
                ?.copyWith(fontWeight: FontWeight.w600, color: AppColors.charcoal),
            titleSmall: base.textTheme.titleSmall
                ?.copyWith(fontWeight: FontWeight.w600, color: AppColors.charcoal),
            headlineMedium: base.textTheme.headlineMedium
                ?.copyWith(fontWeight: FontWeight.w700, color: AppColors.charcoal),
            headlineSmall: base.textTheme.headlineSmall
                ?.copyWith(fontWeight: FontWeight.w700, color: AppColors.charcoal),
            bodyMedium: base.textTheme.bodyMedium?.copyWith(color: AppColors.charcoal),
            bodySmall: base.textTheme.bodySmall?.copyWith(color: AppColors.textMuted),
            labelLarge: base.textTheme.labelLarge?.copyWith(fontWeight: FontWeight.w600),
          ),
      appBarTheme: const AppBarTheme(
        backgroundColor: AppColors.background,
        foregroundColor: AppColors.charcoal,
        surfaceTintColor: Colors.transparent,
        elevation: 0,
        scrolledUnderElevation: 0,
        centerTitle: false,
        titleTextStyle: TextStyle(
          color: AppColors.charcoal,
          fontSize: 20,
          fontWeight: FontWeight.w700,
        ),
      ),
      cardTheme: CardThemeData(
        color: AppColors.surface,
        elevation: 0,
        margin: EdgeInsets.zero,
        shape: RoundedRectangleBorder(
          borderRadius: BorderRadius.circular(AppRadius.md),
          side: const BorderSide(color: AppColors.border),
        ),
      ),
      // The salesperson is standing in a shop, often one-handed. Everything is big enough to hit
      // without looking twice.
      filledButtonTheme: FilledButtonThemeData(
        style: FilledButton.styleFrom(
          minimumSize: const Size.fromHeight(52),
          shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(AppRadius.md)),
          textStyle: const TextStyle(fontSize: 16, fontWeight: FontWeight.w600),
        ),
      ),
      outlinedButtonTheme: OutlinedButtonThemeData(
        style: OutlinedButton.styleFrom(
          minimumSize: const Size.fromHeight(52),
          side: const BorderSide(color: AppColors.border),
          foregroundColor: AppColors.charcoal,
          shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(AppRadius.md)),
          textStyle: const TextStyle(fontSize: 16, fontWeight: FontWeight.w600),
        ),
      ),
      textButtonTheme: TextButtonThemeData(
        style: TextButton.styleFrom(
          foregroundColor: AppColors.goldDark,
          textStyle: const TextStyle(fontWeight: FontWeight.w600),
        ),
      ),
      inputDecorationTheme: InputDecorationTheme(
        filled: true,
        fillColor: AppColors.surfaceAlt,
        hintStyle: const TextStyle(color: AppColors.textMuted),
        contentPadding: const EdgeInsets.symmetric(horizontal: 14, vertical: 14),
        border: OutlineInputBorder(
          borderRadius: BorderRadius.circular(AppRadius.md),
          borderSide: BorderSide.none,
        ),
        enabledBorder: OutlineInputBorder(
          borderRadius: BorderRadius.circular(AppRadius.md),
          borderSide: BorderSide.none,
        ),
        focusedBorder: OutlineInputBorder(
          borderRadius: BorderRadius.circular(AppRadius.md),
          borderSide: const BorderSide(color: AppColors.gold, width: 1.5),
        ),
      ),
      dividerTheme: const DividerThemeData(color: AppColors.border, thickness: 1, space: 1),
      listTileTheme: const ListTileThemeData(
        minVerticalPadding: 12,
        iconColor: AppColors.textMuted,
      ),
      navigationBarTheme: NavigationBarThemeData(
        backgroundColor: AppColors.surface,
        surfaceTintColor: Colors.transparent,
        indicatorColor: Colors.transparent,
        height: 64,
        elevation: 0,
        labelTextStyle: WidgetStateProperty.resolveWith((states) => TextStyle(
              fontSize: 12,
              fontWeight: states.contains(WidgetState.selected) ? FontWeight.w700 : FontWeight.w500,
              color: states.contains(WidgetState.selected) ? AppColors.goldDark : AppColors.textMuted,
            )),
        iconTheme: WidgetStateProperty.resolveWith((states) => IconThemeData(
              color: states.contains(WidgetState.selected) ? AppColors.goldDark : AppColors.textMuted,
            )),
      ),
      snackBarTheme: SnackBarThemeData(
        backgroundColor: AppColors.charcoal,
        contentTextStyle: const TextStyle(color: Colors.white),
        behavior: SnackBarBehavior.floating,
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(AppRadius.sm)),
      ),
      bottomSheetTheme: const BottomSheetThemeData(
        backgroundColor: AppColors.surface,
        surfaceTintColor: Colors.transparent,
        shape: RoundedRectangleBorder(
          borderRadius: BorderRadius.vertical(top: Radius.circular(AppRadius.lg)),
        ),
      ),
      progressIndicatorTheme: const ProgressIndicatorThemeData(color: AppColors.gold),
      floatingActionButtonTheme: const FloatingActionButtonThemeData(
        backgroundColor: AppColors.gold,
        foregroundColor: Colors.white,
        elevation: 1,
        focusElevation: 1,
        hoverElevation: 1,
        highlightElevation: 2,
      ),
      segmentedButtonTheme: SegmentedButtonThemeData(
        style: ButtonStyle(
          backgroundColor: WidgetStateProperty.resolveWith((states) =>
              states.contains(WidgetState.selected) ? AppColors.goldSoft : AppColors.surface),
          foregroundColor: WidgetStateProperty.resolveWith((states) =>
              states.contains(WidgetState.selected) ? AppColors.goldDark : AppColors.charcoal),
          side: const WidgetStatePropertyAll(BorderSide(color: AppColors.border)),
        ),
      ),
      dropdownMenuTheme: DropdownMenuThemeData(
        menuStyle: MenuStyle(
          backgroundColor: const WidgetStatePropertyAll(AppColors.surface),
          surfaceTintColor: const WidgetStatePropertyAll(Colors.transparent),
          elevation: const WidgetStatePropertyAll(3),
          shape: WidgetStatePropertyAll(
            RoundedRectangleBorder(
              borderRadius: BorderRadius.circular(AppRadius.md),
              side: const BorderSide(color: AppColors.border),
            ),
          ),
        ),
      ),
    );
  }
}

/// The small letter-spaced caps label used on every step/section eyebrow: "TODAY SO FAR",
/// "1 · SELECT SHOP", "VAN STOCK". One text style so every screen's labels read as one system.
const TextStyle kEyebrowStyle = TextStyle(
  fontSize: 11.5,
  fontWeight: FontWeight.w700,
  letterSpacing: 0.6,
  color: AppColors.textMuted,
);
