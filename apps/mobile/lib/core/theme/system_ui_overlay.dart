import 'package:flutter/material.dart';
import 'package:flutter/services.dart';

abstract final class TruckNavSystemUi {
  static const Color lightNavigationBar = Color(0xFFE8EFF2);
  static const Color darkNavigationBar = Color(0xFF07131C);

  static SystemUiOverlayStyle forBrightness(
    Brightness brightness, {
    Color? navigationBarColor,
  }) {
    final darkIcons = brightness == Brightness.light;
    final barColor =
        navigationBarColor ??
        (darkIcons ? lightNavigationBar : darkNavigationBar);
    return SystemUiOverlayStyle(
      statusBarColor: Colors.transparent,
      statusBarIconBrightness: darkIcons ? Brightness.dark : Brightness.light,
      statusBarBrightness: brightness,
      systemNavigationBarColor: barColor,
      systemNavigationBarDividerColor: barColor,
      systemNavigationBarIconBrightness: darkIcons
          ? Brightness.dark
          : Brightness.light,
      systemStatusBarContrastEnforced: false,
      systemNavigationBarContrastEnforced: false,
    );
  }
}

class ThemeAwareSystemUi extends StatelessWidget {
  const ThemeAwareSystemUi({required this.child, super.key});

  final Widget child;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return AnnotatedRegion<SystemUiOverlayStyle>(
      key: const Key('app-system-ui-style'),
      value: TruckNavSystemUi.forBrightness(
        theme.brightness,
        navigationBarColor: theme.scaffoldBackgroundColor,
      ),
      child: child,
    );
  }
}
