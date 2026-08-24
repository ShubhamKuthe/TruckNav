import 'dart:async';

import 'session_state.dart';
import 'trucknav_protocol.dart';

sealed class ConnectorConnectResult {
  const ConnectorConnectResult();
}

class ConnectorConnectSuccess extends ConnectorConnectResult {
  const ConnectorConnectSuccess(
    this.peerIdentity, [
    this.visualMapConnection = const VisualMapConnection(
      host: '',
      port: 0,
      accessToken: '',
    ),
  ]);

  final PeerIdentity peerIdentity;
  final VisualMapConnection visualMapConnection;
}

class ConnectorConnectVersionMismatch extends ConnectorConnectResult {
  const ConnectorConnectVersionMismatch(this.message);

  final String message;
}

class ConnectorConnectBusy extends ConnectorConnectResult {
  const ConnectorConnectBusy(this.message);

  final String message;
}

class ConnectorConnectFailure extends ConnectorConnectResult {
  const ConnectorConnectFailure(this.message);

  final String message;
}

sealed class ConnectorClientEvent {
  const ConnectorClientEvent();
}

class ConnectorClientDisconnected extends ConnectorClientEvent {
  const ConnectorClientDisconnected({required this.intentional, this.reason});

  final bool intentional;
  final String? reason;
}

class ConnectorClientWeakConnectionChanged extends ConnectorClientEvent {
  const ConnectorClientWeakConnectionChanged(this.isWeak);

  final bool isWeak;
}

class ConnectorClientGameStateUpdated extends ConnectorClientEvent {
  const ConnectorClientGameStateUpdated(this.gameState);

  final SupportedGameState gameState;
}

class ConnectorClientTelemetryUpdated extends ConnectorClientEvent {
  const ConnectorClientTelemetryUpdated(this.telemetry);

  final NativeTelemetryState telemetry;
}

class ConnectorClientRouteUpdated extends ConnectorClientEvent {
  const ConnectorClientRouteUpdated(this.route);

  final NativeRouteState route;
}

class ConnectorClientRouteProgressUpdated extends ConnectorClientEvent {
  const ConnectorClientRouteProgressUpdated(this.progress);

  final NativeRouteProgress progress;
}

abstract interface class ConnectorClient {
  Stream<ConnectorClientEvent> get events;

  Future<ConnectorConnectResult> connect({
    required LocalDeviceIdentity localDevice,
    required ConnectorSummary connector,
  });

  Future<void> disconnect({bool intentional = true});

  Future<void> requestGameState();

  Future<void> dispose();
}
