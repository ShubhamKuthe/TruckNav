import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:trucknav_mobile/features/session/connector_client.dart';
import 'package:trucknav_mobile/features/session/local_network_permission_service.dart';
import 'package:trucknav_mobile/features/session/mdns_connector_discovery_service.dart';
import 'package:trucknav_mobile/features/session/session_preferences_store.dart';
import 'package:trucknav_mobile/features/session/session_controller.dart';
import 'package:trucknav_mobile/features/session/session_state.dart';
import 'package:trucknav_mobile/features/session/web_socket_connector_client.dart';

import 'test_doubles.dart';

Future<void> _pumpAsyncWork() async {
  await Future<void>.delayed(Duration.zero);
  await Future<void>.delayed(Duration.zero);
}

void main() {
  test('initial discovery deduplicates connectors and marks previous pc', () async {
    final preferences = FakeSessionPreferencesStore(
      snapshot: fakeProfile(lastConnectedDeviceId: 'rock-id'),
    );
    final discovery = FakeDiscoveryService(
      responses: <List<ConnectorSummary>>[
        <ConnectorSummary>[
          fakeConnector(host: '192.168.0.10'),
          fakeConnector(host: '192.168.0.11'),
          fakeConnector(id: 'other-id', name: 'FLEET'),
        ],
      ],
    );
    final client = FakeConnectorClient();

    final container = ProviderContainer(
      overrides: [
        sessionPreferencesStoreProvider.overrideWithValue(preferences),
        connectorDiscoveryServiceProvider.overrideWithValue(discovery),
        connectorClientProvider.overrideWithValue(client),
      ],
    );
    addTearDown(container.dispose);

    container.read(sessionControllerProvider.notifier);
    await _pumpAsyncWork();

    final state = container.read(sessionControllerProvider);
    expect(state.connectionStage, ConnectionStage.discovering);
    expect(state.availableConnectors, hasLength(2));
    expect(state.availableConnectors.first.name, 'ROCK');
    expect(state.availableConnectors.first.isPrevious, isTrue);
    expect(state.availableConnectors.last.name, 'FLEET');
  });

  test('handshake success enters game-selection state and persists connector', () async {
    final preferences = FakeSessionPreferencesStore(snapshot: fakeProfile());
    final discovery = FakeDiscoveryService(
      responses: <List<ConnectorSummary>>[
        <ConnectorSummary>[fakeConnector()],
      ],
    );
    final client = FakeConnectorClient()
      ..enqueueConnectResult(ConnectorConnectSuccess(fakePeerIdentity()));

    final container = ProviderContainer(
      overrides: [
        sessionPreferencesStoreProvider.overrideWithValue(preferences),
        connectorDiscoveryServiceProvider.overrideWithValue(discovery),
        connectorClientProvider.overrideWithValue(client),
      ],
    );
    addTearDown(container.dispose);

    final controller = container.read(sessionControllerProvider.notifier);
    await _pumpAsyncWork();

    final connector = container.read(sessionControllerProvider).availableConnectors.single;
    await controller.connectToConnector(connector);

    final state = container.read(sessionControllerProvider);
    expect(state.connectionStage, ConnectionStage.gameSelection);
    expect(state.selectedConnector?.name, 'ROCK');
    expect(state.lastConnectedDeviceId, 'rock-id');
    expect(preferences.savedLastConnectedConnector?.id, 'rock-id');
    expect(client.connectCalls.single.$1.deviceName, 'HEN');
  });

  test('protocol mismatch moves the ui into update-required state', () async {
    final preferences = FakeSessionPreferencesStore(snapshot: fakeProfile());
    final discovery = FakeDiscoveryService(
      responses: <List<ConnectorSummary>>[
        <ConnectorSummary>[fakeConnector()],
      ],
    );
    final client = FakeConnectorClient()
      ..enqueueConnectResult(
        const ConnectorConnectVersionMismatch('Protocol version mismatch.'),
      );

    final container = ProviderContainer(
      overrides: [
        sessionPreferencesStoreProvider.overrideWithValue(preferences),
        connectorDiscoveryServiceProvider.overrideWithValue(discovery),
        connectorClientProvider.overrideWithValue(client),
      ],
    );
    addTearDown(container.dispose);

    final controller = container.read(sessionControllerProvider.notifier);
    await _pumpAsyncWork();

    await controller.connectToConnector(
      container.read(sessionControllerProvider).availableConnectors.single,
    );

    final state = container.read(sessionControllerProvider);
    expect(state.connectionStage, ConnectionStage.versionMismatch);
    expect(state.connectionError, contains('mismatch'));
  });

  test('unexpected disconnect enters reconnecting but manual disconnect does not', () async {
    final preferences = FakeSessionPreferencesStore(snapshot: fakeProfile());
    final discovery = FakeDiscoveryService(
      responses: <List<ConnectorSummary>>[
        <ConnectorSummary>[fakeConnector()],
      ],
    );
    final client = FakeConnectorClient()
      ..enqueueConnectResult(ConnectorConnectSuccess(fakePeerIdentity()));

    final container = ProviderContainer(
      overrides: [
        sessionPreferencesStoreProvider.overrideWithValue(preferences),
        connectorDiscoveryServiceProvider.overrideWithValue(discovery),
        connectorClientProvider.overrideWithValue(client),
      ],
    );
    addTearDown(container.dispose);

    final controller = container.read(sessionControllerProvider.notifier);
    await _pumpAsyncWork();
    await controller.connectToConnector(
      container.read(sessionControllerProvider).availableConnectors.single,
    );

    client.emitDisconnect(intentional: false, reason: 'wifi lost');
    await _pumpAsyncWork();

    expect(
      container.read(sessionControllerProvider).connectionStage,
      ConnectionStage.reconnecting,
    );

    await controller.disconnect();
    expect(
      container.read(sessionControllerProvider).connectionStage,
      anyOf(ConnectionStage.discovering, ConnectionStage.noPcFound),
    );
    expect(client.disconnectCalls.last, isTrue);
  });

  test('scan again recovers from the no-pc-found state', () async {
    final preferences = FakeSessionPreferencesStore(snapshot: fakeProfile());
    final discovery = FakeDiscoveryService(
      responses: <List<ConnectorSummary>>[
        const <ConnectorSummary>[],
        <ConnectorSummary>[fakeConnector()],
      ],
    );
    final client = FakeConnectorClient();

    final container = ProviderContainer(
      overrides: [
        sessionPreferencesStoreProvider.overrideWithValue(preferences),
        connectorDiscoveryServiceProvider.overrideWithValue(discovery),
        connectorClientProvider.overrideWithValue(client),
      ],
    );
    addTearDown(container.dispose);

    final controller = container.read(sessionControllerProvider.notifier);
    await _pumpAsyncWork();

    expect(
      container.read(sessionControllerProvider).connectionStage,
      ConnectionStage.noPcFound,
    );

    await controller.scanAgain();

    final state = container.read(sessionControllerProvider);
    expect(state.availableConnectors.single.name, 'ROCK');
    expect(state.connectionStage, ConnectionStage.discovering);
    expect(discovery.scanCount, greaterThanOrEqualTo(2));
  });

  test('local network access denial surfaces a dedicated discovery state', () async {
    final preferences = FakeSessionPreferencesStore(snapshot: fakeProfile());
    final discovery = FakeDiscoveryService(
      startException: const LocalNetworkPermissionException(
        'Local network access is required before TruckNav can discover a Connector on this LAN.',
        targetSdkVersion: 37,
        requiresRuntimePermission: true,
      ),
    );
    final client = FakeConnectorClient();

    final container = ProviderContainer(
      overrides: [
        sessionPreferencesStoreProvider.overrideWithValue(preferences),
        connectorDiscoveryServiceProvider.overrideWithValue(discovery),
        connectorClientProvider.overrideWithValue(client),
      ],
    );
    addTearDown(container.dispose);

    container.read(sessionControllerProvider.notifier);
    await _pumpAsyncWork();

    final state = container.read(sessionControllerProvider);
    expect(state.connectionStage, ConnectionStage.localNetworkAccessRequired);
    expect(state.connectionError, contains('Local network access'));
  });
}
