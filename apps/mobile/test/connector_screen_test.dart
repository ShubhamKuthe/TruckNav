import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:trucknav_mobile/features/connector/connector_screen.dart';
import 'package:trucknav_mobile/features/session/session_controller.dart';
import 'package:trucknav_mobile/features/session/session_state.dart';

import 'test_doubles.dart';

void main() {
  testWidgets('connector screen shows no supported game UI', (tester) async {
    await tester.pumpWidget(
      ProviderScope(
        overrides: [
          sessionControllerProvider.overrideWith(
            () => _StaticSessionController(_buildSessionState()),
          ),
        ],
        child: const MaterialApp(home: ConnectorScreen()),
      ),
    );

    expect(find.text('Devices are paired'), findsOneWidget);
    expect(find.text('No supported game detected'), findsOneWidget);
    expect(find.text('EURO TRUCK SIMULATOR 2'), findsAtLeastNWidgets(1));
    expect(find.text('AMERICAN TRUCK SIMULATOR'), findsAtLeastNWidgets(1));
    expect(find.text('Not running'), findsAtLeastNWidgets(2));
  });

  testWidgets('connector screen shows ETS2 as running', (tester) async {
    await tester.pumpWidget(
      ProviderScope(
        overrides: [
          sessionControllerProvider.overrideWith(
            () => _StaticSessionController(
              _buildSessionState(
                supportedGames: const SupportedGameState(
                  ets2Running: true,
                  atsRunning: false,
                ),
              ),
            ),
          ),
        ],
        child: const MaterialApp(home: ConnectorScreen()),
      ),
    );

    expect(find.text('Detected on ROCK'), findsOneWidget);
    expect(find.text('EURO TRUCK SIMULATOR 2'), findsOneWidget);
    expect(find.text('Running'), findsOneWidget);
    expect(find.text('Open GPS'), findsOneWidget);
    expect(find.text('Other Games'), findsOneWidget);
    expect(find.text('AMERICAN TRUCK SIMULATOR'), findsOneWidget);
  });

  testWidgets('connector screen shows ATS as running', (tester) async {
    await tester.pumpWidget(
      ProviderScope(
        overrides: [
          sessionControllerProvider.overrideWith(
            () => _StaticSessionController(
              _buildSessionState(
                supportedGames: const SupportedGameState(
                  ets2Running: false,
                  atsRunning: true,
                ),
              ),
            ),
          ),
        ],
        child: const MaterialApp(home: ConnectorScreen()),
      ),
    );

    expect(find.text('Detected on ROCK'), findsOneWidget);
    expect(find.text('AMERICAN TRUCK SIMULATOR'), findsOneWidget);
    expect(find.text('Running'), findsOneWidget);
    expect(find.text('Open GPS'), findsOneWidget);
    expect(find.text('Other Games'), findsOneWidget);
    expect(find.text('EURO TRUCK SIMULATOR 2'), findsOneWidget);
  });

  testWidgets('connector screen shows both games as running', (tester) async {
    await tester.pumpWidget(
      ProviderScope(
        overrides: [
          sessionControllerProvider.overrideWith(
            () => _StaticSessionController(
              _buildSessionState(
                supportedGames: const SupportedGameState(
                  ets2Running: true,
                  atsRunning: true,
                ),
              ),
            ),
          ),
        ],
        child: const MaterialApp(home: ConnectorScreen()),
      ),
    );

    expect(find.text('EURO TRUCK SIMULATOR 2'), findsOneWidget);
    expect(find.text('AMERICAN TRUCK SIMULATOR'), findsOneWidget);
    expect(find.text('Running'), findsNWidgets(2));
    expect(find.text('Open GPS'), findsNWidgets(2));
    expect(find.text('Other Games'), findsNothing);
  });
}

SessionState _buildSessionState({
  SupportedGameState supportedGames = const SupportedGameState.none(),
  TruckGame selectedGame = TruckGame.none,
}) {
  return SessionState.initial().copyWith(
    deviceId: 'hen-id',
    deviceName: 'HEN',
    selectedConnector: fakeConnector(),
    connectionStage: ConnectionStage.gameSelection,
    supportedGames: supportedGames,
    runningGame: supportedGames.singleRunningGameOrNone,
    selectedGame: selectedGame,
  );
}

class _StaticSessionController extends SessionController {
  _StaticSessionController(this._state);

  final SessionState _state;

  @override
  SessionState build() => _state;
}
