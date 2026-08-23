import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:trucknav_mobile/features/gps/gps_screen.dart';
import 'package:trucknav_mobile/features/session/session_controller.dart';
import 'package:trucknav_mobile/features/session/session_state.dart';

import 'test_doubles.dart';

void main() {
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

      expect(find.text('Zoom in'), findsOneWidget);

      await tester.pump(const Duration(seconds: 3));
      await tester.pump(const Duration(milliseconds: 250));

      expect(find.text('Zoom in'), findsNothing);

      await tester.tap(find.byType(GestureDetector).first);
      await tester.pump(const Duration(milliseconds: 250));

      expect(find.text('Zoom in'), findsOneWidget);
    },
  );

  testWidgets('gps screen shows game-not-detected actions when selected game closes', (
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

    expect(find.text('Game not detected'), findsOneWidget);
    expect(
      find.text('Start Euro Truck Simulator 2 on ROCK to continue.'),
      findsOneWidget,
    );
    expect(find.text('Check Again'), findsOneWidget);
    expect(find.text('Choose Another Game'), findsOneWidget);
  });
}

SessionState _buildSessionState({required TruckGame selectedGame}) {
  return SessionState.initial().copyWith(
    deviceId: 'hen-id',
    deviceName: 'HEN',
    selectedConnector: fakeConnector(),
    connectionStage: ConnectionStage.waitingForStream,
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
