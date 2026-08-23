import 'dart:async';
import 'dart:convert';
import 'dart:developer' as developer;
import 'dart:io';

import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'connector_discovery_service.dart';
import 'local_network_permission_service.dart';
import 'session_state.dart';
import 'trucknav_protocol.dart';

final lanPairingServiceProvider = Provider<LanPairingService>((ref) {
  final service = MulticastLanPairingService(
    localNetworkPermissionService: ref.watch(
      localNetworkPermissionServiceProvider,
    ),
  );
  ref.onDispose(() => unawaited(service.dispose()));
  return service;
});

final connectorDiscoveryServiceProvider = Provider<ConnectorDiscoveryService>((
  ref,
) {
  return ref.watch(lanPairingServiceProvider);
});

class PairRequest {
  const PairRequest({required this.connector, required this.requesterName});

  final ConnectorSummary connector;
  final String requesterName;
}

abstract interface class LanPairingService
    implements ConnectorDiscoveryService {
  Stream<PairRequest> get pairRequests;

  Future<void> updateLocalDevice(LocalDeviceIdentity localDevice);

  Future<void> announceNow();
}

class MulticastLanPairingService implements LanPairingService {
  static const Duration _probeWindow = Duration(milliseconds: 650);
  static const Duration _presenceInterval = Duration(seconds: 2);
  static const Duration _presenceTtl = Duration(seconds: 8);

  MulticastLanPairingService({
    required LocalNetworkPermissionService localNetworkPermissionService,
  }) : _localNetworkPermissionService = localNetworkPermissionService;

  final LocalNetworkPermissionService _localNetworkPermissionService;
  final StreamController<PairRequest> _pairRequestsController =
      StreamController<PairRequest>.broadcast();
  final Map<String, ConnectorSummary> _connectors =
      <String, ConnectorSummary>{};
  final InternetAddress _multicastGroup = InternetAddress(
    truckNavLanPairingGroup,
  );

  RawDatagramSocket? _socket;
  StreamSubscription<RawSocketEvent>? _socketSubscription;
  Timer? _presenceTimer;
  LocalDeviceIdentity? _localDevice;
  bool _started = false;

  @override
  Stream<PairRequest> get pairRequests => _pairRequestsController.stream;

  @override
  Future<void> start() async {
    if (_started) {
      return;
    }

    final access = await _localNetworkPermissionService.ensureDiscoveryAccess();
    final socket = await RawDatagramSocket.bind(
      InternetAddress.anyIPv4,
      truckNavLanPairingPort,
      reuseAddress: true,
    );

    socket.readEventsEnabled = true;
    socket.writeEventsEnabled = false;
    socket.multicastLoopback = false;
    socket.multicastHops = 1;
    socket.joinMulticast(_multicastGroup);

    _socket = socket;
    _socketSubscription = socket.listen(_handleSocketEvent);
    _presenceTimer = Timer.periodic(
      _presenceInterval,
      (_) => unawaited(_sendPresence()),
    );
    _started = true;

    developer.log(
      'LAN pairing started on ${socket.address.address}:${socket.port}; '
      'targetSdk=${access.targetSdkVersion ?? 'unknown'}; '
      'multicastLockHeld=${access.multicastLockHeld}.',
      name: 'TruckNav.Pairing',
    );

    await _sendPresence();
    await _sendProbe();
  }

  @override
  Future<List<ConnectorSummary>> scanOnce() async {
    if (!_started) {
      await start();
    }

    await _sendProbe();
    await _sendPresence();
    await Future<void>.delayed(_probeWindow);
    return _currentConnectors();
  }

  @override
  Future<void> stop() async {
    _presenceTimer?.cancel();
    _presenceTimer = null;
    await _socketSubscription?.cancel();
    _socketSubscription = null;
    _socket?.close();
    _socket = null;
    _started = false;
    _connectors.clear();
    await _localNetworkPermissionService.releaseDiscoveryAccess();
  }

  Future<void> dispose() async {
    await stop();
    await _pairRequestsController.close();
  }

  @override
  Future<void> updateLocalDevice(LocalDeviceIdentity localDevice) async {
    _localDevice = localDevice;
    if (_started) {
      await _sendPresence();
    }
  }

  @override
  Future<void> announceNow() => _sendPresence();

  void _handleSocketEvent(RawSocketEvent event) {
    if (event != RawSocketEvent.read) {
      return;
    }

    final socket = _socket;
    if (socket == null) {
      return;
    }

    Datagram? datagram;
    while ((datagram = socket.receive()) != null) {
      _handleDatagram(datagram!);
    }
  }

  void _handleDatagram(Datagram datagram) {
    try {
      final decoded = jsonDecode(utf8.decode(datagram.data));
      if (decoded is! Map<String, Object?>) {
        return;
      }

      final localDevice = _localDevice;
      final senderDeviceId = decoded['deviceId'] as String?;
      if (localDevice != null && senderDeviceId == localDevice.deviceId) {
        return;
      }

      switch (decoded['kind']) {
        case 'presence':
          _handlePresenceMessage(decoded, datagram.address);
        case 'probe':
          unawaited(_sendPresence());
        case 'pair_request':
          _handlePairRequestMessage(decoded, datagram.address);
      }
    } catch (error, stackTrace) {
      developer.log(
        'Ignoring malformed LAN pairing datagram.',
        name: 'TruckNav.Pairing',
        error: error,
        stackTrace: stackTrace,
      );
    }
  }

  void _handlePresenceMessage(
    Map<String, Object?> payload,
    InternetAddress senderAddress,
  ) {
    if (payload['role'] != 'connector') {
      return;
    }

    final deviceId = payload['deviceId'] as String?;
    final deviceName = payload['deviceName'] as String?;
    final appVersion = payload['appVersion'] as String?;
    final protocolVersion = payload['protocolVersion'] as int?;
    final port = payload['wsPort'] as int?;
    if (deviceId == null ||
        deviceId.isEmpty ||
        deviceName == null ||
        deviceName.isEmpty ||
        appVersion == null ||
        appVersion.isEmpty ||
        protocolVersion == null ||
        port == null) {
      return;
    }

    _connectors[deviceId] = ConnectorSummary(
      id: deviceId,
      name: deviceName,
      host: senderAddress.address,
      addresses: <String>[senderAddress.address],
      port: port,
      protocolVersion: protocolVersion,
      appVersion: appVersion,
      isPrevious: false,
      lastSeen: DateTime.now(),
      platformLabel: 'Windows PC',
      networkLabel: 'Local pairing',
    );
  }

  void _handlePairRequestMessage(
    Map<String, Object?> payload,
    InternetAddress senderAddress,
  ) {
    final localDevice = _localDevice;
    if (localDevice == null ||
        payload['targetDeviceId'] != localDevice.deviceId) {
      return;
    }

    final connectorPayload = payload['connector'];
    if (connectorPayload is! Map<String, Object?>) {
      return;
    }

    final connectorId = connectorPayload['deviceId'] as String?;
    final connectorName = connectorPayload['deviceName'] as String?;
    final appVersion = connectorPayload['appVersion'] as String?;
    final protocolVersion = connectorPayload['protocolVersion'] as int?;
    final port = connectorPayload['wsPort'] as int?;
    if (connectorId == null ||
        connectorId.isEmpty ||
        connectorName == null ||
        connectorName.isEmpty ||
        appVersion == null ||
        appVersion.isEmpty ||
        protocolVersion == null ||
        port == null) {
      return;
    }

    final connector = ConnectorSummary(
      id: connectorId,
      name: connectorName,
      host: senderAddress.address,
      addresses: <String>[senderAddress.address],
      port: port,
      protocolVersion: protocolVersion,
      appVersion: appVersion,
      isPrevious: false,
      lastSeen: DateTime.now(),
      platformLabel: 'Windows PC',
      networkLabel: 'Pair request',
    );

    _connectors[connector.id] = connector;
    _pairRequestsController.add(
      PairRequest(connector: connector, requesterName: connectorName),
    );
  }

  Future<void> _sendProbe() async {
    await _sendMessage(<String, Object?>{
      'kind': 'probe',
      'role': 'mobile',
      'deviceId': _localDevice?.deviceId ?? '',
    });
  }

  Future<void> _sendPresence() async {
    final localDevice = _localDevice;
    if (localDevice == null) {
      return;
    }

    await _sendMessage(<String, Object?>{
      'kind': 'presence',
      'role': 'mobile',
      'deviceId': localDevice.deviceId,
      'deviceName': localDevice.deviceName,
      'platform': 'android',
      'appVersion': truckNavAppVersion,
      'protocolVersion': truckNavProtocolVersion,
    });
  }

  Future<void> _sendMessage(Map<String, Object?> payload) async {
    final socket = _socket;
    if (socket == null) {
      return;
    }

    payload['sentAtUtc'] = DateTime.now().toUtc().toIso8601String();
    final bytes = utf8.encode(jsonEncode(payload));
    socket.send(bytes, _multicastGroup, truckNavLanPairingPort);
  }

  List<ConnectorSummary> _currentConnectors() {
    final cutoff = DateTime.now().subtract(_presenceTtl);
    final expiredIds = <String>[];

    for (final entry in _connectors.entries) {
      if (entry.value.lastSeen.isBefore(cutoff)) {
        expiredIds.add(entry.key);
      }
    }

    for (final expiredId in expiredIds) {
      _connectors.remove(expiredId);
    }

    return _connectors.values.toList(growable: false);
  }
}
