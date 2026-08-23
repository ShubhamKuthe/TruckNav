import 'dart:async';
import 'dart:collection';

import 'package:trucknav_mobile/features/session/connector_client.dart';
import 'package:trucknav_mobile/features/session/connector_discovery_service.dart';
import 'package:trucknav_mobile/features/session/session_preferences_store.dart';
import 'package:trucknav_mobile/features/session/session_state.dart';
import 'package:trucknav_mobile/features/session/trucknav_protocol.dart';

class FakeSessionPreferencesStore implements SessionPreferencesStore {
  FakeSessionPreferencesStore({
    required this.snapshot,
  });

  SessionPreferencesSnapshot snapshot;
  String? savedDeviceName;
  bool? savedAutoReconnect;
  bool? savedAutoOpenRunningGame;
  ConnectorSummary? savedLastConnectedConnector;

  @override
  Future<SessionPreferencesSnapshot> loadOrCreateProfile() async => snapshot;

  @override
  Future<void> saveAutoOpenRunningGame(bool value) async {
    savedAutoOpenRunningGame = value;
  }

  @override
  Future<void> saveAutoReconnect(bool value) async {
    savedAutoReconnect = value;
  }

  @override
  Future<void> saveDeviceName(String deviceName) async {
    savedDeviceName = deviceName;
    snapshot = SessionPreferencesSnapshot(
      deviceId: snapshot.deviceId,
      deviceName: deviceName,
      isFirstLaunch: false,
      autoReconnect: snapshot.autoReconnect,
      autoOpenRunningGame: snapshot.autoOpenRunningGame,
      lastConnectedDeviceId: snapshot.lastConnectedDeviceId,
      lastConnectedDeviceName: snapshot.lastConnectedDeviceName,
    );
  }

  @override
  Future<void> saveLastConnectedConnector(ConnectorSummary connector) async {
    savedLastConnectedConnector = connector;
    snapshot = SessionPreferencesSnapshot(
      deviceId: snapshot.deviceId,
      deviceName: snapshot.deviceName,
      isFirstLaunch: snapshot.isFirstLaunch,
      autoReconnect: snapshot.autoReconnect,
      autoOpenRunningGame: snapshot.autoOpenRunningGame,
      lastConnectedDeviceId: connector.id,
      lastConnectedDeviceName: connector.name,
    );
  }
}

class FakeDiscoveryService implements ConnectorDiscoveryService {
  FakeDiscoveryService({
    List<List<ConnectorSummary>>? responses,
    this.startException,
    this.scanException,
  }) : _responses = Queue<List<ConnectorSummary>>.from(
          responses ?? <List<ConnectorSummary>>[],
        );

  final Queue<List<ConnectorSummary>> _responses;
  final Exception? startException;
  final Exception? scanException;
  int startCount = 0;
  int scanCount = 0;
  int stopCount = 0;

  void enqueueResponse(List<ConnectorSummary> connectors) {
    _responses.add(connectors);
  }

  @override
  Future<List<ConnectorSummary>> scanOnce() async {
    scanCount += 1;
    if (scanException case final exception?) {
      throw exception;
    }

    if (_responses.isEmpty) {
      return const <ConnectorSummary>[];
    }

    if (_responses.length == 1) {
      return _responses.first;
    }

    return _responses.removeFirst();
  }

  @override
  Future<void> start() async {
    startCount += 1;
    if (startException case final exception?) {
      throw exception;
    }
  }

  @override
  Future<void> stop() async {
    stopCount += 1;
  }
}

class FakeConnectorClient implements ConnectorClient {
  final StreamController<ConnectorClientEvent> _events =
      StreamController<ConnectorClientEvent>.broadcast();
  final Queue<ConnectorConnectResult> _connectResults =
      Queue<ConnectorConnectResult>();

  final List<(LocalDeviceIdentity, ConnectorSummary)> connectCalls =
      <(LocalDeviceIdentity, ConnectorSummary)>[];
  final List<bool> disconnectCalls = <bool>[];

  void enqueueConnectResult(ConnectorConnectResult result) {
    _connectResults.add(result);
  }

  void emitDisconnect({required bool intentional, String? reason}) {
    _events.add(
      ConnectorClientDisconnected(intentional: intentional, reason: reason),
    );
  }

  void emitWeakConnection(bool isWeak) {
    _events.add(ConnectorClientWeakConnectionChanged(isWeak));
  }

  @override
  Future<ConnectorConnectResult> connect({
    required LocalDeviceIdentity localDevice,
    required ConnectorSummary connector,
  }) async {
    connectCalls.add((localDevice, connector));
    if (_connectResults.isEmpty) {
      return const ConnectorConnectFailure('No fake connect result queued.');
    }

    return _connectResults.removeFirst();
  }

  @override
  Future<void> disconnect({bool intentional = true}) async {
    disconnectCalls.add(intentional);
  }

  @override
  Future<void> dispose() async {
    await _events.close();
  }

  @override
  Stream<ConnectorClientEvent> get events => _events.stream;
}

SessionPreferencesSnapshot fakeProfile({
  bool isFirstLaunch = false,
  String deviceId = 'hen-id',
  String deviceName = 'HEN',
  bool autoReconnect = true,
  bool autoOpenRunningGame = false,
  String? lastConnectedDeviceId,
  String? lastConnectedDeviceName,
}) {
  return SessionPreferencesSnapshot(
    deviceId: deviceId,
    deviceName: deviceName,
    isFirstLaunch: isFirstLaunch,
    autoReconnect: autoReconnect,
    autoOpenRunningGame: autoOpenRunningGame,
    lastConnectedDeviceId: lastConnectedDeviceId,
    lastConnectedDeviceName: lastConnectedDeviceName,
  );
}

ConnectorSummary fakeConnector({
  String id = 'rock-id',
  String name = 'ROCK',
  String host = '192.168.0.10',
  List<String>? addresses,
  int port = 43111,
  int protocolVersion = 1,
  String appVersion = '0.1.0',
  bool isPrevious = false,
}) {
  return ConnectorSummary(
    id: id,
    name: name,
    host: host,
    addresses: addresses ?? <String>[host],
    port: port,
    protocolVersion: protocolVersion,
    appVersion: appVersion,
    isPrevious: isPrevious,
    lastSeen: DateTime(2026, 8, 23, 12),
  );
}

PeerIdentity fakePeerIdentity({
  String deviceId = 'rock-id',
  String deviceName = 'ROCK',
}) {
  return PeerIdentity(
    protocolVersion: 1,
    appVersion: '0.1.0',
    deviceId: deviceId,
    deviceName: deviceName,
    role: 'connector',
    platform: 'windows',
    capabilities: const <String>[],
  );
}
