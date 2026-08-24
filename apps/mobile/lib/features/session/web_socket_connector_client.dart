import 'dart:async';
import 'dart:developer' as developer;

import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:web_socket_channel/status.dart' as status;
import 'package:web_socket_channel/web_socket_channel.dart';

import 'connector_client.dart';
import 'session_state.dart';
import 'trucknav_protocol.dart';

final connectorClientProvider = Provider<ConnectorClient>((ref) {
  final client = WebSocketConnectorClient();
  ref.onDispose(() => unawaited(client.dispose()));
  return client;
});

class WebSocketConnectorClient implements ConnectorClient {
  static const Duration _connectTimeout = Duration(seconds: 5);
  static const Duration _pingInterval = Duration(seconds: 10);
  static const Duration _weakConnectionThreshold = Duration(milliseconds: 1200);
  static const int _weakSamplesToRaise = 2;
  static const int _healthySamplesToClear = 2;
  static const Duration _pongTimeout = Duration(seconds: 25);

  final StreamController<ConnectorClientEvent> _eventsController =
      StreamController<ConnectorClientEvent>.broadcast();

  WebSocketChannel? _channel;
  StreamSubscription<dynamic>? _subscription;
  Completer<ConnectorConnectResult>? _handshakeCompleter;
  Timer? _pingTimer;
  DateTime? _pendingPingSentAt;
  bool _weakConnection = false;
  bool _intentionalClose = false;
  bool _emitDisconnectOnDone = true;
  String? _disconnectReason;
  String? _activeEndpoint;
  int? _activePort;
  int _degradedPongSamples = 0;
  int _healthyPongSamples = 0;

  @override
  Stream<ConnectorClientEvent> get events => _eventsController.stream;

  @override
  Future<ConnectorConnectResult> connect({
    required LocalDeviceIdentity localDevice,
    required ConnectorSummary connector,
  }) async {
    await disconnect();

    var lastFailure = const ConnectorConnectFailure(
      'Unable to connect to the selected TruckNav Connector.',
    );

    for (final host in _candidateHosts(connector)) {
      final result = await _connectToHost(localDevice, connector, host);
      switch (result) {
        case ConnectorConnectSuccess():
          developer.log(
            'Connected to ${connector.name} through $host.',
            name: 'TruckNav.Session',
          );
          return result;
        case ConnectorConnectBusy():
        case ConnectorConnectVersionMismatch():
          await disconnect(intentional: false);
          return result;
        case ConnectorConnectFailure():
          lastFailure = result;
          await disconnect(intentional: false);
      }
    }

    return lastFailure;
  }

  @override
  Future<void> disconnect({bool intentional = true}) async {
    _intentionalClose = intentional;
    _emitDisconnectOnDone = false;
    _disconnectReason = intentional
        ? truckNavIntentionalDisconnectReason
        : null;
    _stopPingLoop();

    final channel = _channel;
    _channel = null;

    if (channel != null) {
      try {
        await channel.sink.close(
          status.normalClosure,
          intentional ? truckNavIntentionalDisconnectReason : null,
        );
      } catch (_) {}
    }

    await _subscription?.cancel();
    _subscription = null;
    _handshakeCompleter = null;
    _weakConnection = false;
    _pendingPingSentAt = null;
    _activeEndpoint = null;
    _activePort = null;
    _degradedPongSamples = 0;
    _healthyPongSamples = 0;
  }

  @override
  Future<void> requestGameState() async {
    final channel = _channel;
    if (channel == null) {
      return;
    }

    channel.sink.add(encodeGameStateRequestMessage());
  }

  @override
  Future<void> dispose() async {
    await disconnect();
    await _eventsController.close();
  }

  Future<ConnectorConnectResult> _connectToHost(
    LocalDeviceIdentity localDevice,
    ConnectorSummary connector,
    String host,
  ) async {
    _prepareForConnect(host, connector.port);
    final uri = Uri(
      scheme: 'ws',
      host: host,
      port: connector.port,
      path: truckNavWebSocketPath,
    );

    try {
      final channel = _channel = WebSocketChannel.connect(uri);
      await channel.ready.timeout(_connectTimeout);

      _subscription = channel.stream.listen(
        _handleIncomingMessage,
        onDone: _handleSocketDone,
        onError: _handleSocketError,
        cancelOnError: false,
      );

      developer.log(
        'WebSocket connecting to ${connector.name} on $host:${connector.port}.',
        name: 'TruckNav.Session',
      );

      channel.sink.add(encodeHelloMessage(localDevice));
      developer.log(
        'Hello sent to ${connector.name} on $host:${connector.port}.',
        name: 'TruckNav.Session',
      );

      final result = await _handshakeCompleter!.future.timeout(
        _connectTimeout,
        onTimeout: () =>
            const ConnectorConnectFailure('Timed out waiting for hello_ack.'),
      );

      if (result is ConnectorConnectSuccess) {
        _startPingLoop();
      }

      return result;
    } catch (error, stackTrace) {
      developer.log(
        'WebSocket candidate $host:${connector.port} failed.',
        name: 'TruckNav.Session',
        error: error,
        stackTrace: stackTrace,
      );
      return const ConnectorConnectFailure(
        'Unable to connect to the selected TruckNav Connector.',
      );
    }
  }

  void _prepareForConnect(String host, int port) {
    _intentionalClose = false;
    _emitDisconnectOnDone = true;
    _disconnectReason = null;
    _weakConnection = false;
    _pendingPingSentAt = null;
    _activeEndpoint = host;
    _activePort = port;
    _degradedPongSamples = 0;
    _healthyPongSamples = 0;
    _handshakeCompleter = Completer<ConnectorConnectResult>();
  }

  static List<String> _candidateHosts(ConnectorSummary connector) {
    final orderedHosts = <String>[];
    final seen = <String>{};

    void add(String value) {
      final trimmed = value.trim();
      if (trimmed.isEmpty || !seen.add(trimmed)) {
        return;
      }

      orderedHosts.add(trimmed);
    }

    add(connector.host);
    for (final address in connector.addresses) {
      add(address);
    }

    return orderedHosts;
  }

  void _handleIncomingMessage(dynamic message) {
    if (message is! String) {
      if (!(_handshakeCompleter?.isCompleted ?? true)) {
        _handshakeCompleter!.complete(
          const ConnectorConnectFailure(
            'Received a non-text control message from the Connector.',
          ),
        );
      }
      return;
    }

    try {
      final inboundMessage = parseProtocolInboundMessage(message);
      switch (inboundMessage) {
        case HelloAckProtocolMessage(:final peerIdentity):
          if (!(_handshakeCompleter?.isCompleted ?? true)) {
            developer.log(
              'hello_ack received from ${peerIdentity.deviceName} '
              '(${peerIdentity.deviceId}) on ${_activeEndpoint ?? 'unknown'}.',
              name: 'TruckNav.Session',
            );
            _handshakeCompleter!.complete(
              ConnectorConnectSuccess(
                peerIdentity,
                VisualMapConnection(
                  host: _activeEndpoint ?? '',
                  port: _activePort ?? 0,
                  accessToken: peerIdentity.mapAccessToken,
                ),
              ),
            );
          }
        case ErrorProtocolMessage(:final code, :final message):
          if (!(_handshakeCompleter?.isCompleted ?? true)) {
            _handshakeCompleter!.complete(_mapErrorToResult(code, message));
          } else {
            _closeUnexpected(message);
          }
        case PongProtocolMessage():
          _applyPongSample();
        case GameStateProtocolMessage(:final games):
          _eventsController.add(
            ConnectorClientGameStateUpdated(_toSupportedGameState(games)),
          );
        case TelemetryStateProtocolMessage(:final telemetry):
          _eventsController.add(ConnectorClientTelemetryUpdated(telemetry));
        case RouteStateProtocolMessage(:final route):
          _eventsController.add(ConnectorClientRouteUpdated(route));
        case RouteProgressProtocolMessage(:final progress):
          _eventsController.add(ConnectorClientRouteProgressUpdated(progress));
      }
    } on FormatException catch (exception) {
      if (!(_handshakeCompleter?.isCompleted ?? true)) {
        _handshakeCompleter!.complete(
          ConnectorConnectFailure(exception.message),
        );
      } else {
        _closeUnexpected(exception.message);
      }
    }
  }

  void _handleSocketDone() {
    _stopPingLoop();

    if (!(_handshakeCompleter?.isCompleted ?? true)) {
      _handshakeCompleter!.complete(
        const ConnectorConnectFailure(
          'The Connector closed the session before hello_ack was received.',
        ),
      );
    } else if (_emitDisconnectOnDone) {
      _eventsController.add(
        ConnectorClientDisconnected(
          intentional: _intentionalClose,
          reason: _disconnectReason ?? _channel?.closeReason,
        ),
      );
    }

    _cleanupAfterSocketClosure();
  }

  void _handleSocketError(Object error) {
    _stopPingLoop();

    if (!(_handshakeCompleter?.isCompleted ?? true)) {
      _handshakeCompleter!.complete(
        const ConnectorConnectFailure(
          'The Connector control channel failed during hello handshake.',
        ),
      );
    } else {
      _eventsController.add(
        ConnectorClientDisconnected(
          intentional: false,
          reason: error.toString(),
        ),
      );
    }

    _cleanupAfterSocketClosure();
  }

  ConnectorConnectResult _mapErrorToResult(String code, String message) {
    return switch (code) {
      'protocol_mismatch' => ConnectorConnectVersionMismatch(message),
      'busy' => ConnectorConnectBusy(message),
      _ => ConnectorConnectFailure(message),
    };
  }

  void _startPingLoop() {
    _stopPingLoop();

    _pingTimer = Timer.periodic(_pingInterval, (_) {
      final channel = _channel;
      if (channel == null) {
        return;
      }

      final now = DateTime.now();
      final pendingPingSentAt = _pendingPingSentAt;
      if (pendingPingSentAt != null) {
        final elapsed = now.difference(pendingPingSentAt);
        if (elapsed >= _pongTimeout) {
          _closeUnexpected('Pong timeout');
        }
        return;
      }

      try {
        _pendingPingSentAt = now;
        channel.sink.add(encodePingMessage());
      } catch (_) {
        _closeUnexpected('Unable to send ping');
      }
    });
  }

  void _stopPingLoop() {
    _pingTimer?.cancel();
    _pingTimer = null;
  }

  void _applyPongSample() {
    final pendingPingSentAt = _pendingPingSentAt;
    _pendingPingSentAt = null;

    if (pendingPingSentAt == null) {
      return;
    }

    final rtt = DateTime.now().difference(pendingPingSentAt);
    if (rtt >= _weakConnectionThreshold) {
      _degradedPongSamples += 1;
      _healthyPongSamples = 0;
      if (_degradedPongSamples >= _weakSamplesToRaise && !_weakConnection) {
        _weakConnection = true;
        _eventsController.add(const ConnectorClientWeakConnectionChanged(true));
      }
      return;
    }

    _degradedPongSamples = 0;
    if (_weakConnection) {
      _healthyPongSamples += 1;
      if (_healthyPongSamples >= _healthySamplesToClear) {
        _weakConnection = false;
        _eventsController.add(
          const ConnectorClientWeakConnectionChanged(false),
        );
      }
      return;
    }

    _healthyPongSamples = 0;
  }

  void _closeUnexpected(String reason) {
    _disconnectReason = reason;
    _intentionalClose = false;
    _emitDisconnectOnDone = true;
    _stopPingLoop();

    final channel = _channel;
    if (channel == null) {
      return;
    }

    try {
      channel.sink.close(status.goingAway, reason);
    } catch (_) {
      _eventsController.add(
        ConnectorClientDisconnected(intentional: false, reason: reason),
      );
      _cleanupAfterSocketClosure();
    }
  }

  void _cleanupAfterSocketClosure() {
    _channel = null;
    _subscription = null;
    _handshakeCompleter = null;
    _weakConnection = false;
    _pendingPingSentAt = null;
    _activeEndpoint = null;
    _activePort = null;
    _degradedPongSamples = 0;
    _healthyPongSamples = 0;
  }

  static SupportedGameState _toSupportedGameState(
    List<ProtocolGameStateEntry> games,
  ) {
    var ets2Running = false;
    var atsRunning = false;

    for (final game in games) {
      switch (game.game) {
        case 'ets2':
          ets2Running = game.running;
        case 'ats':
          atsRunning = game.running;
      }
    }

    return SupportedGameState(ets2Running: ets2Running, atsRunning: atsRunning);
  }
}
