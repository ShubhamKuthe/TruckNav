import 'dart:convert';

const int truckNavProtocolVersion = 1;
const String truckNavAppVersion = '0.1.0';
const String truckNavDiscoveryServiceType = '_trucknav._tcp.local';
const String truckNavWebSocketPath = '/ws';
const String truckNavIntentionalDisconnectReason = 'intentional_disconnect';

class LocalDeviceIdentity {
  const LocalDeviceIdentity({
    required this.deviceId,
    required this.deviceName,
  });

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
        'capabilities': const <String>[],
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
    );
  }

  final int protocolVersion;
  final String appVersion;
  final String deviceId;
  final String deviceName;
  final String role;
  final String platform;
  final List<String> capabilities;
}

sealed class ProtocolInboundMessage {
  const ProtocolInboundMessage();
}

class HelloAckProtocolMessage extends ProtocolInboundMessage {
  const HelloAckProtocolMessage(this.peerIdentity);

  final PeerIdentity peerIdentity;
}

class ErrorProtocolMessage extends ProtocolInboundMessage {
  const ErrorProtocolMessage({
    required this.code,
    required this.message,
  });

  final String code;
  final String message;
}

class PongProtocolMessage extends ProtocolInboundMessage {
  const PongProtocolMessage();
}

ProtocolInboundMessage parseProtocolInboundMessage(String rawMessage) {
  final decoded = jsonDecode(rawMessage);
  if (decoded is! Map<String, Object?>) {
    throw const FormatException('Protocol payload must be a JSON object.');
  }

  final type = decoded['type'];
  if (type is! String || type.isEmpty) {
    throw const FormatException("Protocol payload is missing the string 'type'.");
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
    default:
      throw FormatException("Unsupported inbound protocol message type '$type'.");
  }
}

String encodeHelloMessage(LocalDeviceIdentity localDevice) =>
    jsonEncode(localDevice.toHelloMessage());

String encodePingMessage() =>
    jsonEncode(const <String, Object?>{'type': 'ping'});
