import 'package:flutter/material.dart';

abstract final class TruckNavPalette {
  static const Color background = Color(0xFF0D1014);
  static const Color surface = Color(0xFF151A20);
  static const Color surfaceAlt = Color(0xFF1B222A);
  static const Color surfaceRaised = Color(0xFF222B35);
  static const Color accent = Color(0xFFF59E32);
  static const Color success = Color(0xFF3ECF8E);
  static const Color error = Color(0xFFE5484D);
  static const Color textPrimary = Color(0xFFF4F7FB);
  static const Color textSecondary = Color(0xFF9DA8B4);
  static const Color border = Color(0xFF2D3641);
}

abstract final class TruckNavTheme {
  static ThemeData get theme {
    const colorScheme = ColorScheme(
      brightness: Brightness.dark,
      primary: TruckNavPalette.accent,
      onPrimary: TruckNavPalette.background,
      secondary: TruckNavPalette.success,
      onSecondary: TruckNavPalette.background,
      error: TruckNavPalette.error,
      onError: TruckNavPalette.textPrimary,
      surface: TruckNavPalette.surface,
      onSurface: TruckNavPalette.textPrimary,
    );

    final base = ThemeData(
      useMaterial3: true,
      colorScheme: colorScheme,
      scaffoldBackgroundColor: TruckNavPalette.background,
      fontFamily: 'sans-serif',
    );

    return base.copyWith(
      textTheme: base.textTheme.apply(
        bodyColor: TruckNavPalette.textPrimary,
        displayColor: TruckNavPalette.textPrimary,
      ),
      cardTheme: const CardThemeData(
        color: TruckNavPalette.surface,
        elevation: 0,
        shape: RoundedRectangleBorder(
          borderRadius: BorderRadius.all(Radius.circular(28)),
          side: BorderSide(color: TruckNavPalette.border),
        ),
      ),
      appBarTheme: const AppBarTheme(
        backgroundColor: Colors.transparent,
        foregroundColor: TruckNavPalette.textPrimary,
        elevation: 0,
      ),
      chipTheme: base.chipTheme.copyWith(
        side: const BorderSide(color: TruckNavPalette.border),
        selectedColor: TruckNavPalette.accent.withValues(alpha: 0.18),
      ),
      dividerColor: TruckNavPalette.border,
      switchTheme: SwitchThemeData(
        thumbColor: WidgetStateProperty.resolveWith((states) {
          if (states.contains(WidgetState.selected)) {
            return TruckNavPalette.accent;
          }
          return TruckNavPalette.textSecondary;
        }),
        trackColor: WidgetStateProperty.resolveWith((states) {
          if (states.contains(WidgetState.selected)) {
            return TruckNavPalette.accent.withValues(alpha: 0.32);
          }
          return TruckNavPalette.surfaceRaised;
        }),
      ),
    );
  }
}
