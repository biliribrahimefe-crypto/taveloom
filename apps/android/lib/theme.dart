import 'package:flutter/material.dart';

const _accent = Color(0xFF6370F2);

class TavColors {
  const TavColors({
    required this.bg,
    required this.surface,
    required this.surfaceAlt,
    required this.border,
    required this.muted,
    required this.success,
    required this.warning,
    required this.danger,
  });

  final Color bg, surface, surfaceAlt, border, muted, success, warning, danger;

  static const dark = TavColors(
    bg: Color(0xFF0F1115),
    surface: Color(0xFF171A21),
    surfaceAlt: Color(0xFF1E222B),
    border: Color(0xFF2A2F3A),
    muted: Color(0xFF9AA1AF),
    success: Color(0xFF3DD68C),
    warning: Color(0xFFF5B83D),
    danger: Color(0xFFF2615C),
  );
  static const light = TavColors(
    bg: Color(0xFFF4F5F8),
    surface: Color(0xFFFFFFFF),
    surfaceAlt: Color(0xFFF0F2F6),
    border: Color(0xFFDDE1E9),
    muted: Color(0xFF5B6372),
    success: Color(0xFF12935A),
    warning: Color(0xFFB7791F),
    danger: Color(0xFFCC3B36),
  );

  static TavColors of(BuildContext c) =>
      Theme.of(c).brightness == Brightness.dark ? dark : light;
}

ThemeData buildTheme(Brightness b) {
  final t = b == Brightness.dark ? TavColors.dark : TavColors.light;
  final scheme = ColorScheme.fromSeed(
    seedColor: _accent,
    brightness: b,
  ).copyWith(primary: _accent, surface: t.surface, error: t.danger);
  return ThemeData(
    useMaterial3: true,
    colorScheme: scheme,
    scaffoldBackgroundColor: t.bg,
    appBarTheme: AppBarTheme(
      backgroundColor: t.bg,
      elevation: 0,
      scrolledUnderElevation: 0,
      centerTitle: false,
    ),
    filledButtonTheme: FilledButtonThemeData(
      style: FilledButton.styleFrom(
        minimumSize: const Size.fromHeight(52),
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(14)),
      ),
    ),
    outlinedButtonTheme: OutlinedButtonThemeData(
      style: OutlinedButton.styleFrom(
        minimumSize: const Size.fromHeight(52),
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(14)),
      ),
    ),
    inputDecorationTheme: InputDecorationTheme(
      filled: true,
      fillColor: t.surfaceAlt,
      border: OutlineInputBorder(
        borderRadius: BorderRadius.circular(12),
        borderSide: BorderSide(color: t.border),
      ),
      enabledBorder: OutlineInputBorder(
        borderRadius: BorderRadius.circular(12),
        borderSide: BorderSide(color: t.border),
      ),
    ),
    snackBarTheme: SnackBarThemeData(
      behavior: SnackBarBehavior.floating,
      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12)),
    ),
  );
}
