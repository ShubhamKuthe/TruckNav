import 'dart:convert';

import 'package:flutter_test/flutter_test.dart';

import 'package:trucknav_mobile/features/session/trucknav_protocol.dart';

void main() {
  test('parseProtocolInboundMessage parses game_state', () {
    const payload = '''
    {
      "type": "game_state",
      "games": [
        { "game": "ets2", "running": true },
        { "game": "ats", "running": false }
      ]
    }
    ''';

    final message = parseProtocolInboundMessage(payload);

    expect(message, isA<GameStateProtocolMessage>());
    final gameState = message as GameStateProtocolMessage;
    expect(gameState.games, hasLength(2));
    expect(gameState.games.first.game, 'ets2');
    expect(gameState.games.first.running, isTrue);
    expect(gameState.games.last.game, 'ats');
    expect(gameState.games.last.running, isFalse);
  });

  test('encodeGameStateRequestMessage writes a typed request', () {
    final payload =
        jsonDecode(encodeGameStateRequestMessage()) as Map<String, dynamic>;

    expect(payload['type'], 'game_state_request');
  });

  test('parseProtocolInboundMessage parses native telemetry', () {
    const payload = '''
    {
      "type": "telemetry_state",
      "game": "ets2",
      "connected": true,
      "paused": false,
      "x": 12.5,
      "z": -7.5,
      "headingDegrees": 90,
      "speedKph": 80,
      "speedLimitKph": 90,
      "onJob": true,
      "navigationStatus": "Navigating",
      "statusMessage": "Turn right"
    }
    ''';

    final message =
        parseProtocolInboundMessage(payload) as TelemetryStateProtocolMessage;

    expect(message.telemetry.connected, isTrue);
    expect(message.telemetry.game.name, 'euroTruckSimulator2');
    expect(message.telemetry.x, 12.5);
    expect(message.telemetry.speedKph, 80);
  });

  test('parseProtocolInboundMessage parses route geometry and progress', () {
    const routePayload = '''
    {
      "type": "route_state",
      "revision": 3,
      "available": true,
      "routeId": "route-1",
      "game": "ats",
      "destination": "Rail Export, Phoenix",
      "points": [{"x": 0, "z": 0}, {"x": 100, "z": 50}],
      "steps": [{
        "maneuver": "Right",
        "instruction": "Turn right",
        "pointIndex": 1,
        "distanceFromStartMeters": 111.8
      }],
      "contextRoads": [{
        "fromX": 0,
        "fromZ": 0,
        "toX": 100,
        "toZ": 50,
        "isMajor": true,
        "isFerry": false
      }],
      "mapPoints": [{
        "type": "Fuel",
        "x": 40,
        "z": 20,
        "label": null,
        "isDestination": false
      }],
      "mapLabels": [{
        "text": "Phoenix",
        "x": 100,
        "z": 50,
        "isDestination": true
      }],
      "totalDistanceMeters": 111.8,
      "estimatedTimeSeconds": 12,
      "isReroute": false
    }
    ''';
    const progressPayload = '''
    {
      "type": "route_progress",
      "routeId": "route-1",
      "segmentIndex": 0,
      "projectedX": 25,
      "projectedZ": 12.5,
      "distanceFromRouteMeters": 2,
      "remainingDistanceMeters": 80,
      "remainingTimeSeconds": 9,
      "nextManeuver": "Right",
      "nextInstruction": "Turn right",
      "distanceToNextManeuverMeters": 80,
      "isOffRoute": false,
      "teleportDetected": false
    }
    ''';

    final route =
        parseProtocolInboundMessage(routePayload) as RouteStateProtocolMessage;
    final progress =
        parseProtocolInboundMessage(progressPayload)
            as RouteProgressProtocolMessage;

    expect(route.route.revision, 3);
    expect(route.route.points, hasLength(2));
    expect(route.route.steps.single.instruction, 'Turn right');
    expect(route.route.contextRoads.single.isMajor, isTrue);
    expect(route.route.mapPoints.single.type, 'Fuel');
    expect(route.route.mapLabels.single.text, 'Phoenix');
    expect(progress.progress.routeId, 'route-1');
    expect(progress.progress.remainingDistanceMeters, 80);
  });
}
