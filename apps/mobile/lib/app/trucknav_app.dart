import 'package:flutter/material.dart';

import '../core/theme/trucknav_theme.dart';
import '../core/theme/system_ui_overlay.dart';
import 'router.dart';

class TruckNavApp extends StatelessWidget {
  const TruckNavApp({super.key});

  @override
  Widget build(BuildContext context) {
    return MaterialApp.router(
      title: 'TruckNav',
      debugShowCheckedModeBanner: false,
      theme: TruckNavTheme.theme,
      builder: (context, child) =>
          ThemeAwareSystemUi(child: child ?? const SizedBox.shrink()),
      routerConfig: appRouter,
    );
  }
}
