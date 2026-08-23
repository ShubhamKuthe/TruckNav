import 'dart:async';
import 'dart:developer' as developer;
import 'dart:io';

import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:multicast_dns/multicast_dns.dart';

import 'connector_address_selection.dart';
import 'connector_discovery_service.dart';
import 'local_network_permission_service.dart';
import 'session_state.dart';
import 'trucknav_protocol.dart';

final connectorDiscoveryServiceProvider = Provider<ConnectorDiscoveryService>((
  ref,
) {
  final service = MdnsConnectorDiscoveryService(
    localNetworkPermissionService: ref.watch(
      localNetworkPermissionServiceProvider,
    ),
  );
  ref.onDispose(() => unawaited(service.stop()));
  return service;
});

class MdnsConnectorDiscoveryService implements ConnectorDiscoveryService {
  static const Duration _lookupTimeout = Duration(milliseconds: 1800);

  MdnsConnectorDiscoveryService({
    required this._localNetworkPermissionService,
  });

  final MDnsClient _client = MDnsClient();
  final LocalNetworkPermissionService _localNetworkPermissionService;
  bool _started = false;

  @override
  Future<void> start() async {
    if (_started) {
      return;
    }

    final access = await _localNetworkPermissionService.ensureDiscoveryAccess();
    await _client.start();
    _started = true;

    developer.log(
      'Discovery started for $_serviceTypeLogLabel on Android targetSdk '
      '${access.targetSdkVersion ?? 'unknown'}; multicast lock held='
      '${access.multicastLockHeld}.',
      name: 'TruckNav.Discovery',
    );
  }

  @override
  Future<List<ConnectorSummary>> scanOnce() async {
    if (!_started) {
      await start();
    }

    final services = <String, ConnectorSummary>{};
    final ptrRecords = await _collect<PtrResourceRecord>(
      _client.lookup<PtrResourceRecord>(
        ResourceRecordQuery.serverPointer(truckNavDiscoveryServiceType),
        timeout: _lookupTimeout,
      ),
    );

    for (final ptrRecord in ptrRecords) {
      final connector = await _resolveConnector(ptrRecord.domainName);
      if (connector != null) {
        services[connector.id] = connector;
      }
    }

    return services.values.toList(growable: false);
  }

  @override
  Future<void> stop() async {
    if (!_started) {
      await _localNetworkPermissionService.releaseDiscoveryAccess();
      return;
    }

    _client.stop();
    _started = false;
    await _localNetworkPermissionService.releaseDiscoveryAccess();
  }

  Future<ConnectorSummary?> _resolveConnector(String fullyQualifiedName) async {
    developer.log(
      'ROCK service discovered via PTR: $fullyQualifiedName',
      name: 'TruckNav.Discovery',
    );

    final srvRecords = await _collect<SrvResourceRecord>(
      _client.lookup<SrvResourceRecord>(
        ResourceRecordQuery.service(fullyQualifiedName),
        timeout: _lookupTimeout,
      ),
    );

    if (srvRecords.isEmpty) {
      return null;
    }

    final txtRecords = await _collect<TxtResourceRecord>(
      _client.lookup<TxtResourceRecord>(
        ResourceRecordQuery.text(fullyQualifiedName),
        timeout: _lookupTimeout,
      ),
    );

    final metadata = _txtRecordsToMap(txtRecords);
    if (metadata['role'] != 'connector') {
      return null;
    }

    final deviceId = metadata['deviceId'];
    final deviceName = metadata['deviceName'];
    final appVersion = metadata['appVersion'];
    final protocolVersion = int.tryParse(metadata['protocolVersion'] ?? '');

    if (deviceId == null ||
        deviceId.isEmpty ||
        deviceName == null ||
        deviceName.isEmpty ||
        appVersion == null ||
        protocolVersion == null) {
      return null;
    }

    final srvRecord = srvRecords.first;
    final addresses = await _resolveHostAddresses(srvRecord.target);
    if (addresses.isEmpty) {
      return null;
    }

    developer.log(
      'Service resolved for $deviceName ($deviceId). Target=${srvRecord.target} '
      'port=${srvRecord.port} selectedAddress=${addresses.first}.',
      name: 'TruckNav.Discovery',
    );

    return ConnectorSummary(
      id: deviceId,
      name: deviceName,
      host: addresses.first,
      addresses: addresses,
      port: srvRecord.port,
      protocolVersion: protocolVersion,
      appVersion: appVersion,
      lastSeen: DateTime.now(),
      isPrevious: false,
    );
  }

  Future<List<String>> _resolveHostAddresses(String hostName) async {
    final ipv4Addresses = await _collect<IPAddressResourceRecord>(
      _client.lookup<IPAddressResourceRecord>(
        ResourceRecordQuery.addressIPv4(hostName),
        timeout: _lookupTimeout,
      ),
    );

    final ipv6Addresses = await _collect<IPAddressResourceRecord>(
      _client.lookup<IPAddressResourceRecord>(
        ResourceRecordQuery.addressIPv6(hostName),
        timeout: _lookupTimeout,
      ),
    );

    return rankConnectorAddresses(
      <InternetAddress>[
        ...ipv4Addresses.map((record) => record.address),
        ...ipv6Addresses.map((record) => record.address),
      ],
      fallbackHost: hostName,
    );
  }

  static Map<String, String> _txtRecordsToMap(List<TxtResourceRecord> records) {
    final metadata = <String, String>{};

    for (final record in records) {
      final separatorIndex = record.text.indexOf('=');
      if (separatorIndex <= 0) {
        continue;
      }

      final key = record.text.substring(0, separatorIndex);
      final value = record.text.substring(separatorIndex + 1);
      metadata[key] = value;
    }

    return metadata;
  }

  static Future<List<TRecord>> _collect<TRecord>(
    Stream<TRecord> stream,
  ) async {
    final results = <TRecord>[];
    await for (final record in stream) {
      results.add(record);
    }
    return results;
  }

  static const String _serviceTypeLogLabel = '_trucknav._tcp.local';
}
