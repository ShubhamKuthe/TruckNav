import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter/services.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:shared_preferences/shared_preferences.dart';

import 'package:trucknav_mobile/features/gps/gps_screen.dart';
import 'package:trucknav_mobile/features/gps/native_navigation_map.dart';
import 'package:trucknav_mobile/features/session/session_controller.dart';
import 'package:trucknav_mobile/features/session/session_state.dart';

import 'test_doubles.dart';

void main() {
  setUp(() => SharedPreferences.setMockInitialValues(<String, Object>{}));

  testWidgets(
    'gps controls auto-hide after three seconds and reappear on tap',
    (tester) async {
      await tester.pumpWidget(
        ProviderScope(
          overrides: [
            sessionControllerProvider.overrideWith(
              () => _StaticSessionController(
                _buildSessionState(selectedGame: TruckGame.euroTruckSimulator2),
              ),
            ),
          ],
          child: const MaterialApp(home: GpsScreen()),
        ),
      );

      expect(find.byKey(const Key('gps-zoom-in')), findsOneWidget);

      await tester.pump(const Duration(seconds: 3));
      await tester.pump(const Duration(milliseconds: 250));

      expect(find.byKey(const Key('gps-zoom-in')), findsNothing);

      await tester.tap(find.byKey(const Key('native-gps-gesture')));
      await tester.pump(const Duration(milliseconds: 400));

      expect(find.byKey(const Key('gps-zoom-in')), findsOneWidget);
    },
  );

  testWidgets('zoom-out is silent and activates manual override', (
    tester,
  ) async {
    await tester.pumpWidget(
      ProviderScope(
        overrides: [
          sessionControllerProvider.overrideWith(
            () => _StaticSessionController(
              _buildSessionState(selectedGame: TruckGame.euroTruckSimulator2),
            ),
          ),
        ],
        child: const MaterialApp(home: GpsScreen()),
      ),
    );

    await tester.tap(find.byKey(const Key('gps-zoom-out')));
    await tester.pump();

    final map = tester.widget<SmoothedNativeNavigationMap>(
      find.byType(SmoothedNativeNavigationMap),
    );
    expect(map.zoom, lessThan(1));
    expect(map.manualZoomOverride, isTrue);
    expect(find.byType(SnackBar), findsNothing);
  });

  testWidgets('two-finger pinch together zooms out continuously', (
    tester,
  ) async {
    await tester.pumpWidget(
      ProviderScope(
        overrides: [
          sessionControllerProvider.overrideWith(
            () => _StaticSessionController(
              _buildSessionState(selectedGame: TruckGame.euroTruckSimulator2),
            ),
          ),
        ],
        child: const MaterialApp(home: GpsScreen()),
      ),
    );

    final target = find.byKey(const Key('native-gps-gesture'));
    final center = tester.getCenter(target);
    final left = await tester.startGesture(
      center - const Offset(60, 0),
      pointer: 1,
    );
    final right = await tester.startGesture(
      center + const Offset(60, 0),
      pointer: 2,
    );
    await left.moveTo(center - const Offset(24, 0));
    await right.moveTo(center + const Offset(24, 0));
    await tester.pump();

    final map = tester.widget<SmoothedNativeNavigationMap>(
      find.byType(SmoothedNativeNavigationMap),
    );
    expect(map.zoom, lessThan(1));
    expect(map.manualZoomOverride, isTrue);
    expect(find.byType(SnackBar), findsNothing);

    await left.up();
    await right.up();
    await tester.pump(const Duration(milliseconds: 100));
  });

  testWidgets('manual zoom persists until the driver explicitly recenters', (
    tester,
  ) async {
    await tester.pumpWidget(
      ProviderScope(
        overrides: [
          sessionControllerProvider.overrideWith(
            () => _StaticSessionController(
              _buildSessionState(selectedGame: TruckGame.euroTruckSimulator2),
            ),
          ),
        ],
        child: const MaterialApp(home: GpsScreen()),
      ),
    );

    await tester.tap(find.byKey(const Key('gps-zoom-out')));
    await tester.pump();
    expect(
      tester
          .widget<SmoothedNativeNavigationMap>(
            find.byType(SmoothedNativeNavigationMap),
          )
          .manualZoomOverride,
      isTrue,
    );

    await tester.pump(const Duration(seconds: 20));
    var map = tester.widget<SmoothedNativeNavigationMap>(
      find.byType(SmoothedNativeNavigationMap),
    );
    expect(map.manualZoomOverride, isTrue);
    expect(map.zoom, lessThan(1));

    await tester.tap(find.byKey(const Key('native-gps-gesture')));
    await tester.pump(const Duration(milliseconds: 300));
    await tester.tap(find.byKey(const Key('gps-recenter')));
    await tester.pump();
    map = tester.widget<SmoothedNativeNavigationMap>(
      find.byType(SmoothedNativeNavigationMap),
    );
    expect(map.manualZoomOverride, isFalse);
    expect(map.zoom, closeTo(1, 0.001));
  });

  testWidgets('default close zoom is the maximum and map theme toggles', (
    tester,
  ) async {
    await tester.pumpWidget(
      ProviderScope(
        overrides: [
          sessionControllerProvider.overrideWith(
            () => _StaticSessionController(
              _buildSessionState(selectedGame: TruckGame.euroTruckSimulator2),
            ),
          ),
        ],
        child: const MaterialApp(home: GpsScreen()),
      ),
    );

    final zoomIn = tester.widget<IconButton>(
      find.byKey(const Key('gps-zoom-in')),
    );
    expect(zoomIn.onPressed, isNull);
    expect(
      tester
          .widget<SmoothedNativeNavigationMap>(
            find.byType(SmoothedNativeNavigationMap),
          )
          .darkMap,
      isTrue,
    );
    var systemUi = tester.widget<AnnotatedRegion<SystemUiOverlayStyle>>(
      find.byKey(const Key('gps-system-ui-style')),
    );
    expect(systemUi.value.statusBarIconBrightness, Brightness.light);
    expect(systemUi.value.systemNavigationBarIconBrightness, Brightness.light);

    await tester.tap(find.byKey(const Key('gps-menu')));
    await tester.pumpAndSettle();
    await tester.tap(find.byKey(const Key('gps-map-theme-toggle')));
    await tester.pumpAndSettle();
    expect(
      tester
          .widget<SmoothedNativeNavigationMap>(
            find.byType(SmoothedNativeNavigationMap),
          )
          .darkMap,
      isFalse,
    );
    systemUi = tester.widget<AnnotatedRegion<SystemUiOverlayStyle>>(
      find.byKey(const Key('gps-system-ui-style')),
    );
    expect(systemUi.value.statusBarIconBrightness, Brightness.dark);
    expect(systemUi.value.systemNavigationBarIconBrightness, Brightness.dark);
  });

  testWidgets('map dock filters visual POIs and can clear the filter', (
    tester,
  ) async {
    await tester.pumpWidget(
      ProviderScope(
        overrides: [
          sessionControllerProvider.overrideWith(
            () => _StaticSessionController(
              _buildSessionState(selectedGame: TruckGame.euroTruckSimulator2),
            ),
          ),
        ],
        child: const MaterialApp(home: GpsScreen()),
      ),
    );

    expect(find.byKey(const Key('gps-map-dock')), findsOneWidget);
    await tester.tap(find.byKey(const Key('gps-poi-fuel')));
    await tester.pump();
    var map = tester.widget<SmoothedNativeNavigationMap>(
      find.byType(SmoothedNativeNavigationMap),
    );
    expect(map.visiblePoiTypes, <String>{'Fuel'});

    await tester.tap(find.byKey(const Key('gps-poi-fuel')));
    await tester.pump();
    map = tester.widget<SmoothedNativeNavigationMap>(
      find.byType(SmoothedNativeNavigationMap),
    );
    expect(map.visiblePoiTypes, isNull);
  });

  testWidgets('gps chrome stays usable on a narrow phone viewport', (
    tester,
  ) async {
    tester.view.physicalSize = const Size(320, 700);
    tester.view.devicePixelRatio = 1;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    await tester.pumpWidget(
      ProviderScope(
        overrides: [
          sessionControllerProvider.overrideWith(
            () => _StaticSessionController(
              _buildSessionState(selectedGame: TruckGame.euroTruckSimulator2),
            ),
          ),
        ],
        child: const MaterialApp(home: GpsScreen()),
      ),
    );

    expect(find.byKey(const Key('gps-map-dock')), findsOneWidget);
    expect(find.byKey(const Key('gps-menu')), findsOneWidget);
    expect(find.byKey(const Key('gps-control-rail')), findsOneWidget);
    expect(find.byKey(const Key('gps-zoom-in')), findsOneWidget);
    expect(find.byKey(const Key('gps-zoom-out')), findsOneWidget);
    expect(find.byKey(const Key('gps-recenter')), findsOneWidget);
    expect(tester.takeException(), isNull);
  });

  testWidgets('overview control fits route and recenter restores heading-up', (
    tester,
  ) async {
    await tester.pumpWidget(
      ProviderScope(
        overrides: [
          sessionControllerProvider.overrideWith(
            () => _StaticSessionController(
              _buildSessionState(selectedGame: TruckGame.euroTruckSimulator2),
            ),
          ),
        ],
        child: const MaterialApp(home: GpsScreen()),
      ),
    );

    await tester.tap(find.byKey(const Key('gps-overview')));
    await tester.pump();
    expect(
      tester
          .widget<SmoothedNativeNavigationMap>(
            find.byType(SmoothedNativeNavigationMap),
          )
          .viewMode,
      NavigationViewMode.overview,
    );

    await tester.tap(find.byKey(const Key('gps-overview')));
    await tester.pump();
    expect(
      tester
          .widget<SmoothedNativeNavigationMap>(
            find.byType(SmoothedNativeNavigationMap),
          )
          .viewMode,
      NavigationViewMode.headingUp,
    );
  });

  testWidgets(
    'gps screen shows game-not-detected actions when selected game closes',
    (tester) async {
      await tester.pumpWidget(
        ProviderScope(
          overrides: [
            sessionControllerProvider.overrideWith(
              () => _StaticSessionController(
                _buildSessionState(selectedGame: TruckGame.euroTruckSimulator2),
              ),
            ),
          ],
          child: const MaterialApp(home: GpsScreen()),
        ),
      );

      expect(find.text('Game not detected'), findsOneWidget);
      expect(
        find.text('Start Euro Truck Simulator 2 on ROCK to continue.'),
        findsOneWidget,
      );
      expect(find.text('Check Again'), findsOneWidget);
      expect(find.text('Choose Another Game'), findsOneWidget);
    },
  );
}

SessionState _buildSessionState({required TruckGame selectedGame}) {
  return SessionState.initial().copyWith(
    deviceId: 'hen-id',
    deviceName: 'HEN',
    selectedConnector: fakeConnector(),
    connectionStage: ConnectionStage.waitingForNavigation,
    supportedGames: const SupportedGameState.none(),
    runningGame: TruckGame.none,
    selectedGame: selectedGame,
  );
}

class _StaticSessionController extends SessionController {
  _StaticSessionController(this._state);

  final SessionState _state;

  @override
  SessionState build() => _state;
}
