import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:trucknav_mobile/features/gps/gps_screen.dart';
import 'package:trucknav_mobile/features/session/mdns_connector_discovery_service.dart';
import 'package:trucknav_mobile/features/session/session_preferences_store.dart';
import 'package:trucknav_mobile/features/session/web_socket_connector_client.dart';

import 'test_doubles.dart';

void main() {
  testWidgets(
    'gps controls auto-hide after three seconds and reappear on tap',
    (tester) async {
      final preferences = FakeSessionPreferencesStore(
        snapshot: fakeProfile(isFirstLaunch: true),
      );
      final discovery = FakeDiscoveryService();
      final client = FakeConnectorClient();

      await tester.pumpWidget(
        ProviderScope(
          overrides: [
            sessionPreferencesStoreProvider.overrideWithValue(preferences),
            lanPairingServiceProvider.overrideWithValue(discovery),
            connectorClientProvider.overrideWithValue(client),
          ],
          child: const MaterialApp(home: GpsScreen()),
        ),
      );

      expect(find.text('Zoom in'), findsOneWidget);

      await tester.pump(const Duration(seconds: 3));
      await tester.pump(const Duration(milliseconds: 250));

      expect(find.text('Zoom in'), findsNothing);

      await tester.tap(find.byType(GestureDetector).first);
      await tester.pump(const Duration(milliseconds: 250));

      expect(find.text('Zoom in'), findsOneWidget);
    },
  );
}
