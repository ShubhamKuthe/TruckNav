import 'dart:async';

import 'session_state.dart';
import 'trucknav_protocol.dart';

sealed class ConnectorConnectResult {
  const ConnectorConnectResult();
}

class ConnectorConnectSuccess extends ConnectorConnectResult {
  const ConnectorConnectSuccess(this.peerIdentity);

  final PeerIdentity peerIdentity;
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
  const ConnectorClientDisconnected({
    required this.intentional,
    this.reason,
  });

  final bool intentional;
  final String? reason;
}

class ConnectorClientWeakConnectionChanged extends ConnectorClientEvent {
  const ConnectorClientWeakConnectionChanged(this.isWeak);

  final bool isWeak;
}

abstract interface class ConnectorClient {
  Stream<ConnectorClientEvent> get events;

  Future<ConnectorConnectResult> connect({
    required LocalDeviceIdentity localDevice,
    required ConnectorSummary connector,
  });

  Future<void> disconnect({bool intentional = true});

  Future<void> dispose();
}
