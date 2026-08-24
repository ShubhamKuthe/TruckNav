import 'dart:convert';

import 'session_state.dart';

const int truckNavProtocolVersion = 2;
const String truckNavAppVersion = '0.1.0';
const String truckNavDiscoveryServiceType = '_trucknav._tcp.local';
const String truckNavWebSocketPath = '/ws';
const String truckNavIntentionalDisconnectReason = 'intentional_disconnect';
const String truckNavLanPairingGroup = '239.255.18.18';
const int truckNavLanPairingPort = 43112;

class LocalDeviceIdentity {
  const LocalDeviceIdentity({required this.deviceId, required this.deviceName});

  final String deviceId;
  final String deviceName;

  Map<String, Object?> toHelloMessage() => <String, Object?>{
    'type': 'hello',
    'protocolVersion': truckNavProtocolVersion,
    'appVersion': truckNavAppVersion,
    'deviceId': deviceId,
    'deviceName': deviceName,
    'role': 'mobile',
    'platform': 'android',
    'capabilities': const <String>['native_navigation'],
  };
}

class PeerIdentity {
  const PeerIdentity({
    required this.protocolVersion,
    required this.appVersion,
    required this.deviceId,
    required this.deviceName,
    required this.role,
    required this.platform,
    required this.capabilities,
    this.mapAccessToken = '',
  });

  factory PeerIdentity.fromJson(Map<String, Object?> json) {
    final capabilities = (json['capabilities'] as List<dynamic>? ?? const [])
        .whereType<String>()
        .toList(growable: false);

    return PeerIdentity(
      protocolVersion: json['protocolVersion'] as int? ?? 0,
      appVersion: json['appVersion'] as String? ?? '',
      deviceId: json['deviceId'] as String? ?? '',
      deviceName: json['deviceName'] as String? ?? '',
      role: json['role'] as String? ?? '',
      platform: json['platform'] as String? ?? '',
      capabilities: capabilities,
      mapAccessToken: json['mapAccessToken'] as String? ?? '',
    );
  }

  final int protocolVersion;
  final String appVersion;
  final String deviceId;
  final String deviceName;
  final String role;
  final String platform;
  final List<String> capabilities;
  final String mapAccessToken;
}

sealed class ProtocolInboundMessage {
  const ProtocolInboundMessage();
}

class HelloAckProtocolMessage extends ProtocolInboundMessage {
  const HelloAckProtocolMessage(this.peerIdentity);

  final PeerIdentity peerIdentity;
}

class ErrorProtocolMessage extends ProtocolInboundMessage {
  const ErrorProtocolMessage({required this.code, required this.message});

  final String code;
  final String message;
}

class PongProtocolMessage extends ProtocolInboundMessage {
  const PongProtocolMessage();
}

class ProtocolGameStateEntry {
  const ProtocolGameStateEntry({required this.game, required this.running});

  final String game;
  final bool running;
}

class GameStateProtocolMessage extends ProtocolInboundMessage {
  const GameStateProtocolMessage(this.games);

  final List<ProtocolGameStateEntry> games;
}

class TelemetryStateProtocolMessage extends ProtocolInboundMessage {
  const TelemetryStateProtocolMessage(this.telemetry);

  final NativeTelemetryState telemetry;
}

class RouteStateProtocolMessage extends ProtocolInboundMessage {
  const RouteStateProtocolMessage(this.route);

  final NativeRouteState route;
}

class RouteProgressProtocolMessage extends ProtocolInboundMessage {
  const RouteProgressProtocolMessage(this.progress);

  final NativeRouteProgress progress;
}

ProtocolInboundMessage parseProtocolInboundMessage(String rawMessage) {
  final decoded = jsonDecode(rawMessage);
  if (decoded is! Map<String, Object?>) {
    throw const FormatException('Protocol payload must be a JSON object.');
  }

  final type = decoded['type'];
  if (type is! String || type.isEmpty) {
    throw const FormatException(
      "Protocol payload is missing the string 'type'.",
    );
  }

  switch (type) {
    case 'hello_ack':
      return HelloAckProtocolMessage(PeerIdentity.fromJson(decoded));
    case 'error':
      return ErrorProtocolMessage(
        code: decoded['code'] as String? ?? 'unknown_error',
        message: decoded['message'] as String? ?? 'Unknown protocol error.',
      );
    case 'pong':
      return const PongProtocolMessage();
    case 'game_state':
      final games = (decoded['games'] as List<dynamic>? ?? const <dynamic>[])
          .whereType<Map>()
          .map((game) {
            final payload = Map<String, Object?>.from(game);
            return ProtocolGameStateEntry(
              game: payload['game'] as String? ?? '',
              running: payload['running'] as bool? ?? false,
            );
          })
          .toList(growable: false);
      return GameStateProtocolMessage(games);
    case 'telemetry_state':
      return TelemetryStateProtocolMessage(_parseTelemetry(decoded));
    case 'route_state':
      return RouteStateProtocolMessage(_parseRoute(decoded));
    case 'route_progress':
      return RouteProgressProtocolMessage(_parseProgress(decoded));
    default:
      throw FormatException(
        "Unsupported inbound protocol message type '$type'.",
      );
  }
}

NativeTelemetryState _parseTelemetry(Map<String, Object?> json) {
  return NativeTelemetryState(
    game: _parseGame(json['game']),
    connected: json['connected'] as bool? ?? false,
    paused: json['paused'] as bool? ?? false,
    x: _double(json['x']),
    z: _double(json['z']),
    elevation: _double(json['elevation']),
    headingDegrees: _double(json['headingDegrees']),
    speedKph: _double(json['speedKph']),
    speedLimitKph: _double(json['speedLimitKph']),
    onJob: json['onJob'] as bool? ?? false,
    destinationCity: json['destinationCity'] as String?,
    destinationCompany: json['destinationCompany'] as String?,
    gameNavigationDistanceMeters: _double(json['gameNavigationDistanceMeters']),
    gameNavigationTimeSeconds: _double(json['gameNavigationTimeSeconds']),
    navigationStatus:
        json['navigationStatus'] as String? ?? 'WaitingForTelemetry',
    statusMessage: json['statusMessage'] as String? ?? '',
    timestampUtc: _date(json['timestampUtc']),
  );
}

NativeRouteState _parseRoute(Map<String, Object?> json) {
  final points = (json['points'] as List<dynamic>? ?? const <dynamic>[])
      .whereType<Map>()
      .map((value) {
        final point = Map<String, Object?>.from(value);
        return NativeRoutePoint(_double(point['x']), _double(point['z']));
      })
      .toList(growable: false);
  final steps = (json['steps'] as List<dynamic>? ?? const <dynamic>[])
      .whereType<Map>()
      .map((value) {
        final step = Map<String, Object?>.from(value);
        return NativeRouteStep(
          maneuver: step['maneuver'] as String? ?? 'Continue',
          instruction: step['instruction'] as String? ?? '',
          pointIndex: (step['pointIndex'] as num?)?.toInt() ?? 0,
          distanceFromStartMeters: _double(step['distanceFromStartMeters']),
        );
      })
      .toList(growable: false);
  final contextRoads =
      (json['contextRoads'] as List<dynamic>? ?? const <dynamic>[])
          .whereType<Map>()
          .map((value) {
            final road = Map<String, Object?>.from(value);
            return NativeContextRoad(
              fromX: _double(road['fromX']),
              fromZ: _double(road['fromZ']),
              toX: _double(road['toX']),
              toZ: _double(road['toZ']),
              isMajor: road['isMajor'] as bool? ?? false,
              isFerry: road['isFerry'] as bool? ?? false,
            );
          })
          .toList(growable: false);
  final mapPoints = (json['mapPoints'] as List<dynamic>? ?? const <dynamic>[])
      .whereType<Map>()
      .map((value) {
        final point = Map<String, Object?>.from(value);
        return NativeMapPoint(
          type: point['type'] as String? ?? 'Company',
          x: _double(point['x']),
          z: _double(point['z']),
          label: point['label'] as String?,
          isDestination: point['isDestination'] as bool? ?? false,
        );
      })
      .toList(growable: false);
  final mapLabels = (json['mapLabels'] as List<dynamic>? ?? const <dynamic>[])
      .whereType<Map>()
      .map((value) {
        final label = Map<String, Object?>.from(value);
        return NativeMapLabel(
          text: label['text'] as String? ?? '',
          x: _double(label['x']),
          z: _double(label['z']),
          isDestination: label['isDestination'] as bool? ?? false,
        );
      })
      .where((label) => label.text.isNotEmpty)
      .toList(growable: false);

  return NativeRouteState(
    revision: (json['revision'] as num?)?.toInt() ?? 0,
    available: json['available'] as bool? ?? false,
    routeId: json['routeId'] as String?,
    game: _parseGame(json['game']),
    destination: json['destination'] as String?,
    points: points,
    steps: steps,
    contextRoads: contextRoads,
    mapPoints: mapPoints,
    mapLabels: mapLabels,
    totalDistanceMeters: _double(json['totalDistanceMeters']),
    estimatedTimeSeconds: _double(json['estimatedTimeSeconds']),
    calculatedUtc: _date(json['calculatedUtc']),
    isReroute: json['isReroute'] as bool? ?? false,
  );
}

NativeRouteProgress _parseProgress(Map<String, Object?> json) {
  return NativeRouteProgress(
    routeId: json['routeId'] as String? ?? '',
    segmentIndex: (json['segmentIndex'] as num?)?.toInt() ?? 0,
    projectedX: _double(json['projectedX']),
    projectedZ: _double(json['projectedZ']),
    distanceFromRouteMeters: _double(json['distanceFromRouteMeters']),
    remainingDistanceMeters: _double(json['remainingDistanceMeters']),
    remainingTimeSeconds: _double(json['remainingTimeSeconds']),
    nextManeuver: json['nextManeuver'] as String? ?? 'Continue',
    nextInstruction: json['nextInstruction'] as String? ?? '',
    distanceToNextManeuverMeters: _double(json['distanceToNextManeuverMeters']),
    isOffRoute: json['isOffRoute'] as bool? ?? false,
    teleportDetected: json['teleportDetected'] as bool? ?? false,
    timestampUtc: _date(json['timestampUtc']),
  );
}

TruckGame _parseGame(Object? value) => switch (value) {
  'ets2' => TruckGame.euroTruckSimulator2,
  'ats' => TruckGame.americanTruckSimulator,
  _ => TruckGame.none,
};

double _double(Object? value) => (value as num?)?.toDouble() ?? 0;

DateTime? _date(Object? value) => switch (value) {
  final String text => DateTime.tryParse(text),
  _ => null,
};

String encodeHelloMessage(LocalDeviceIdentity localDevice) =>
    jsonEncode(localDevice.toHelloMessage());

String encodePingMessage() =>
    jsonEncode(const <String, Object?>{'type': 'ping'});

String encodeGameStateRequestMessage() =>
    jsonEncode(const <String, Object?>{'type': 'game_state_request'});
