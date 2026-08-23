import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:trucknav_mobile/features/discovery/discovery_screen.dart';
import 'package:trucknav_mobile/features/session/mdns_connector_discovery_service.dart';
import 'package:trucknav_mobile/features/session/session_preferences_store.dart';
import 'package:trucknav_mobile/features/session/session_state.dart';
import 'package:trucknav_mobile/features/session/web_socket_connector_client.dart';

import 'test_doubles.dart';

void main() {
  testWidgets('discovery screen shows ROCK and the PREVIOUS badge', (
    tester,
  ) async {
    final preferences = FakeSessionPreferencesStore(
      snapshot: fakeProfile(lastConnectedDeviceId: 'rock-id'),
    );
    final discovery = FakeDiscoveryService(
      responses: <List<ConnectorSummary>>[
        <ConnectorSummary>[fakeConnector()],
      ],
    );
    final client = FakeConnectorClient();

    await tester.pumpWidget(
      ProviderScope(
        overrides: [
          sessionPreferencesStoreProvider.overrideWithValue(preferences),
          connectorDiscoveryServiceProvider.overrideWithValue(discovery),
          connectorClientProvider.overrideWithValue(client),
        ],
        child: const MaterialApp(home: DiscoveryScreen()),
      ),
    );

    await tester.pump();
    await tester.pump();

    expect(find.text('ROCK'), findsOneWidget);
    expect(find.text('Connect'), findsOneWidget);
    expect(find.text('PREVIOUS'), findsOneWidget);
  });
}
