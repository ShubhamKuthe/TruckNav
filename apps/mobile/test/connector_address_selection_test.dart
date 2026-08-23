import 'dart:io';

import 'package:flutter_test/flutter_test.dart';
import 'package:trucknav_mobile/features/session/connector_address_selection.dart';

void main() {
  test('rankConnectorAddresses prefers private IPv4 before usable IPv6', () {
    final ranked = rankConnectorAddresses(
      <InternetAddress>[
        InternetAddress('2001:db8::20'),
        InternetAddress('192.168.0.10'),
        InternetAddress('10.0.0.15'),
      ],
    );

    expect(ranked, <String>['10.0.0.15', '192.168.0.10', '2001:db8::20']);
  });

  test('rankConnectorAddresses drops unusable IPv6 link-local addresses', () {
    final ranked = rankConnectorAddresses(
      <InternetAddress>[
        InternetAddress('fe80::1234'),
        InternetAddress('192.168.0.22'),
      ],
      fallbackHost: 'rock-host.local',
    );

    expect(ranked, <String>['192.168.0.22', 'rock-host.local']);
  });
}
