import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:trucknav_mobile/core/theme/system_ui_overlay.dart';

void main() {
  test('light system UI uses transparent status bar and dark icons', () {
    final style = TruckNavSystemUi.forBrightness(Brightness.light);

    expect(style.statusBarColor, Colors.transparent);
    expect(style.statusBarIconBrightness, Brightness.dark);
    expect(style.statusBarBrightness, Brightness.light);
    expect(style.systemNavigationBarColor, TruckNavSystemUi.lightNavigationBar);
    expect(style.systemNavigationBarIconBrightness, Brightness.dark);
    expect(style.systemStatusBarContrastEnforced, isFalse);
    expect(style.systemNavigationBarContrastEnforced, isFalse);
  });

  test('dark system UI uses transparent status bar and light icons', () {
    final style = TruckNavSystemUi.forBrightness(Brightness.dark);

    expect(style.statusBarColor, Colors.transparent);
    expect(style.statusBarIconBrightness, Brightness.light);
    expect(style.statusBarBrightness, Brightness.dark);
    expect(style.systemNavigationBarColor, TruckNavSystemUi.darkNavigationBar);
    expect(style.systemNavigationBarIconBrightness, Brightness.light);
    expect(style.systemStatusBarContrastEnforced, isFalse);
    expect(style.systemNavigationBarContrastEnforced, isFalse);
  });

  testWidgets('theme-aware overlay rebuilds when theme brightness changes', (
    tester,
  ) async {
    Future<void> pump(Brightness brightness) async {
      await tester.pumpWidget(
        MaterialApp(
          theme: ThemeData(brightness: brightness),
          builder: (context, child) =>
              ThemeAwareSystemUi(child: child ?? const SizedBox.shrink()),
          home: const SizedBox.shrink(),
        ),
      );
      await tester.pumpAndSettle();
    }

    await pump(Brightness.light);
    var region = tester.widget<AnnotatedRegion<SystemUiOverlayStyle>>(
      find.byKey(const Key('app-system-ui-style')),
    );
    expect(region.value.statusBarIconBrightness, Brightness.dark);
    expect(region.value.systemNavigationBarIconBrightness, Brightness.dark);

    await pump(Brightness.dark);
    region = tester.widget<AnnotatedRegion<SystemUiOverlayStyle>>(
      find.byKey(const Key('app-system-ui-style')),
    );
    expect(region.value.statusBarIconBrightness, Brightness.light);
    expect(region.value.systemNavigationBarIconBrightness, Brightness.light);
  });
}
