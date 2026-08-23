import 'dart:async';
import 'dart:developer' as developer;
import 'dart:math';

import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'connector_client.dart';
import 'connector_discovery_service.dart';
import 'local_network_permission_service.dart';
import 'mdns_connector_discovery_service.dart';
import 'session_preferences_store.dart';
import 'session_state.dart';
import 'trucknav_protocol.dart';
import 'web_socket_connector_client.dart';

final sessionControllerProvider =
    NotifierProvider<SessionController, SessionState>(SessionController.new);

class SessionController extends Notifier<SessionState> {
  static const List<int> _reconnectSeconds = <int>[1, 2, 3, 5, 5];
  static const Duration _discoveryInterval = Duration(seconds: 4);

  late final SessionPreferencesStore _preferencesStore;
  late final ConnectorDiscoveryService _discoveryService;
  late final ConnectorClient _connectorClient;
  final Random _random = Random();

  StreamSubscription<ConnectorClientEvent>? _clientEventsSubscription;
  Timer? _discoveryTimer;
  Timer? _reconnectTimer;
  bool _initialized = false;
  bool _discoveryStarted = false;
  bool _scanInFlight = false;
  int _reconnectAttempt = 0;

  @override
  SessionState build() {
    _preferencesStore = ref.watch(sessionPreferencesStoreProvider);
    _discoveryService = ref.watch(connectorDiscoveryServiceProvider);
    _connectorClient = ref.watch(connectorClientProvider);

    _clientEventsSubscription ??=
        _connectorClient.events.listen(_handleConnectorClientEvent);

    ref.onDispose(() {
      _discoveryTimer?.cancel();
      _reconnectTimer?.cancel();
      unawaited(_clientEventsSubscription?.cancel());
      unawaited(_discoveryService.stop());
    });

    unawaited(_initialize());
    return SessionState.initial();
  }

  Future<void> _initialize() async {
    if (_initialized) {
      return;
    }

    _initialized = true;
    final profile = await _preferencesStore.loadOrCreateProfile();

    state = state.copyWith(
      deviceId: profile.deviceId,
      deviceName: profile.deviceName,
      autoReconnect: profile.autoReconnect,
      autoOpenRunningGame: profile.autoOpenRunningGame,
      lastConnectedDeviceId: profile.lastConnectedDeviceId,
      connectionStage: profile.isFirstLaunch
          ? ConnectionStage.namingDevice
          : ConnectionStage.discovering,
    );

    if (!profile.isFirstLaunch) {
      await activateDiscovery();
    }
  }

  Future<void> activateDiscovery() async {
    if (state.connectionStage == ConnectionStage.initializing) {
      return;
    }

    if (!_discoveryStarted) {
      try {
        await _discoveryService.start();
      } on LocalNetworkPermissionException catch (exception) {
        state = state.copyWith(
          connectionStage: ConnectionStage.localNetworkAccessRequired,
          connectionError: exception.message,
          hasCompletedInitialScan: true,
        );
        developer.log(
          'Discovery blocked: ${exception.message}',
          name: 'TruckNav.Session',
        );
        return;
      }

      _discoveryStarted = true;
      developer.log('Discovery started.', name: 'TruckNav.Session');
      _discoveryTimer = Timer.periodic(
        _discoveryInterval,
        (_) => unawaited(_performDiscoveryScan()),
      );
    }

    await _performDiscoveryScan();
  }

  Future<void> completeFirstLaunch(String deviceName) async {
    final normalizedName = deviceName.trim().isEmpty ? 'HEN' : deviceName.trim();
    await _preferencesStore.saveDeviceName(normalizedName);

    state = state.copyWith(
      deviceName: normalizedName,
      connectionStage: ConnectionStage.discovering,
      connectionError: null,
    );

    await activateDiscovery();
  }

  Future<void> scanAgain() async {
    if (!_discoveryStarted) {
      await activateDiscovery();
      return;
    }

    if (state.connectionStage != ConnectionStage.connecting &&
        state.connectionStage != ConnectionStage.reconnecting) {
      state = state.copyWith(
        connectionStage: ConnectionStage.discovering,
        connectionError: null,
      );
    }

    await _performDiscoveryScan();
  }

  Future<void> connectToConnector(
    ConnectorSummary connector, {
    bool reconnectAttempt = false,
  }) async {
    final previousStage = state.connectionStage;
    _reconnectTimer?.cancel();
    developer.log(
      reconnectAttempt
          ? 'Reconnect attempt to ${connector.name} (${connector.id}).'
          : 'Connecting to ${connector.name} (${connector.id}).',
      name: 'TruckNav.Session',
    );

    state = state.copyWith(
      selectedConnector: connector,
      connectionStage: reconnectAttempt
          ? ConnectionStage.reconnecting
          : ConnectionStage.connecting,
      connectionError: null,
      weakConnection: false,
      notice: null,
    );

    final result = await _connectorClient.connect(
      localDevice: LocalDeviceIdentity(
        deviceId: state.deviceId,
        deviceName: state.deviceName,
      ),
      connector: connector,
    );

    switch (result) {
      case ConnectorConnectSuccess():
        _reconnectAttempt = 0;
        await _preferencesStore.saveLastConnectedConnector(connector);
        developer.log(
          'Connected to ${connector.name} (${connector.id}).',
          name: 'TruckNav.Session',
        );
        state = state.copyWith(
          availableConnectors: _markPreviousConnector(
            state.availableConnectors,
            connector.id,
          ),
          selectedConnector: connector.copyWith(
            isPrevious: true,
            lastSeen: DateTime.now(),
          ),
          connectionStage: ConnectionStage.gameSelection,
          runningGame: TruckGame.none,
          selectedGame: TruckGame.none,
          weakConnection: false,
          lastConnectedDeviceId: connector.id,
          connectionError: null,
          notice: previousStage == ConnectionStage.reconnecting
              ? const SessionNotice(
                  kind: SessionNoticeKind.success,
                  message: 'Connection restored',
                )
              : null,
        );
      case ConnectorConnectVersionMismatch(:final message):
        developer.log(
          'Version mismatch while connecting to ${connector.name}: $message',
          name: 'TruckNav.Session',
        );
        state = state.copyWith(
          connectionStage: ConnectionStage.versionMismatch,
          connectionError: message,
          weakConnection: false,
        );
      case ConnectorConnectBusy(:final message):
      case ConnectorConnectFailure(:final message):
        developer.log(
          'Connection attempt failed for ${connector.name}: $message',
          name: 'TruckNav.Session',
        );
        state = state.copyWith(
          connectionStage: reconnectAttempt
              ? ConnectionStage.reconnecting
              : ConnectionStage.connectionFailed,
          connectionError: message,
          weakConnection: false,
        );

        if (reconnectAttempt && state.autoReconnect) {
          _scheduleReconnect();
        }
    }
  }

  void chooseGame(TruckGame game) {
    state = state.copyWith(selectedGame: game);
  }

  void openGps() {
    state = state.copyWith(
      connectionStage: ConnectionStage.waitingForStream,
      lastCommandLabel: null,
    );
  }

  void returnToGameSelection() {
    state = state.copyWith(connectionStage: ConnectionStage.gameSelection);
  }

  Future<void> disconnect() async {
    _reconnectTimer?.cancel();
    _reconnectAttempt = 0;
    await _connectorClient.disconnect(intentional: true);
    developer.log('Intentional disconnect requested.', name: 'TruckNav.Session');

    state = state.copyWith(
      selectedConnector: null,
      runningGame: TruckGame.none,
      selectedGame: TruckGame.none,
      weakConnection: false,
      connectionError: null,
      connectionStage: state.availableConnectors.isEmpty
          ? ConnectionStage.noPcFound
          : ConnectionStage.discovering,
    );

    await _performDiscoveryScan();
  }

  void toggleAutoReconnect(bool value) {
    state = state.copyWith(autoReconnect: value);
    unawaited(_preferencesStore.saveAutoReconnect(value));
  }

  void toggleAutoOpenRunningGame(bool value) {
    state = state.copyWith(autoOpenRunningGame: value);
    unawaited(_preferencesStore.saveAutoOpenRunningGame(value));
  }

  void sendCommand(NavigationCommandKind command) {
    final label = commandLabel(command);
    final notice = command == NavigationCommandKind.cycleNavigationView
        ? const SessionNotice(
            kind: SessionNoticeKind.info,
            message: 'Navigation view placeholder control fired',
          )
        : SessionNotice(kind: SessionNoticeKind.info, message: label);

    state = state.copyWith(lastCommandLabel: label, notice: notice);
  }

  void clearNotice() {
    state = state.copyWith(notice: null);
  }

  Future<void> _performDiscoveryScan() async {
    if (_scanInFlight) {
      return;
    }

    _scanInFlight = true;
    try {
      final discovered = <String, ConnectorSummary>{};
      for (final connector in await _discoveryService.scanOnce()) {
        discovered[connector.id] = connector;
      }

      final updated = _sortConnectors(
        _markPreviousConnector(
          discovered.values.toList(growable: false),
          state.lastConnectedDeviceId,
        ),
      );

      final selectedConnector = state.selectedConnector == null
          ? null
          : updated.cast<ConnectorSummary?>().firstWhere(
                (connector) => connector?.id == state.selectedConnector!.id,
                orElse: () => state.selectedConnector,
              );

      var nextStage = state.connectionStage;
      if (state.connectionStage == ConnectionStage.discovering ||
          state.connectionStage == ConnectionStage.noPcFound ||
          state.connectionStage == ConnectionStage.connectionFailed ||
          state.connectionStage == ConnectionStage.versionMismatch) {
        nextStage = updated.isEmpty
            ? ConnectionStage.noPcFound
            : ConnectionStage.discovering;
      }

      state = state.copyWith(
        availableConnectors: updated,
        selectedConnector: selectedConnector,
        connectionStage: nextStage,
        hasCompletedInitialScan: true,
      );
    } on LocalNetworkPermissionException catch (exception) {
      state = state.copyWith(
        availableConnectors: const <ConnectorSummary>[],
        selectedConnector: null,
        connectionStage: ConnectionStage.localNetworkAccessRequired,
        connectionError: exception.message,
        hasCompletedInitialScan: true,
      );
      developer.log(
        'Discovery blocked during scan: ${exception.message}',
        name: 'TruckNav.Session',
      );
    } catch (error, stackTrace) {
      state = state.copyWith(
        connectionStage: ConnectionStage.connectionFailed,
        connectionError:
            'TruckNav could not complete local discovery on this network.',
        hasCompletedInitialScan: true,
      );
      developer.log(
        'Discovery scan failed.',
        name: 'TruckNav.Session',
        error: error,
        stackTrace: stackTrace,
      );
    } finally {
      _scanInFlight = false;
    }
  }

  void _handleConnectorClientEvent(ConnectorClientEvent event) {
    switch (event) {
      case ConnectorClientWeakConnectionChanged(:final isWeak):
        state = state.copyWith(
          weakConnection: isWeak,
          notice: isWeak
              ? const SessionNotice(
                  kind: SessionNoticeKind.warning,
                  message: 'Weak connection',
                )
              : const SessionNotice(
                  kind: SessionNoticeKind.success,
                  message: 'Connection restored',
                ),
        );
      case ConnectorClientDisconnected(
          :final intentional,
          :final reason,
        ):
        if (intentional) {
          return;
        }

        developer.log(
          'Connector disconnected unexpectedly: ${reason ?? 'unknown reason'}.',
          name: 'TruckNav.Session',
        );
        if (state.autoReconnect && state.selectedConnector != null) {
          state = state.copyWith(
            connectionStage: ConnectionStage.reconnecting,
            weakConnection: false,
            connectionError: reason,
          );
          _scheduleReconnect();
        } else {
          state = state.copyWith(
            connectionStage: ConnectionStage.connectionFailed,
            weakConnection: false,
            connectionError: reason,
          );
        }
    }
  }

  void _scheduleReconnect() {
    _reconnectTimer?.cancel();

    final connectorId = state.selectedConnector?.id ?? state.lastConnectedDeviceId;
    if (connectorId == null) {
      return;
    }

    final seconds =
        _reconnectSeconds[min(_reconnectAttempt, _reconnectSeconds.length - 1)];
    _reconnectAttempt += 1;
    final jitter = Duration(milliseconds: _random.nextInt(300));
    developer.log(
      'Reconnect attempt $_reconnectAttempt scheduled in ${seconds}s.',
      name: 'TruckNav.Session',
    );
    _reconnectTimer = Timer(Duration(seconds: seconds) + jitter, () {
      unawaited(_attemptReconnect(connectorId));
    });
  }

  Future<void> _attemptReconnect(String connectorId) async {
    if (state.connectionStage != ConnectionStage.reconnecting) {
      return;
    }

    final connector = _findConnectorById(connectorId);
    if (connector == null) {
      _scheduleReconnect();
      return;
    }

    await connectToConnector(connector, reconnectAttempt: true);
  }

  ConnectorSummary? _findConnectorById(String connectorId) {
    for (final connector in state.availableConnectors) {
      if (connector.id == connectorId) {
        return connector;
      }
    }

    return null;
  }

  static List<ConnectorSummary> _markPreviousConnector(
    List<ConnectorSummary> connectors,
    String? previousConnectorId,
  ) {
    return connectors
        .map(
          (connector) => connector.copyWith(
            isPrevious:
                previousConnectorId != null && connector.id == previousConnectorId,
          ),
        )
        .toList(growable: false);
  }

  static List<ConnectorSummary> _sortConnectors(List<ConnectorSummary> connectors) {
    final sorted = connectors.toList(growable: false);
    sorted.sort((left, right) {
      if (left.isPrevious != right.isPrevious) {
        return left.isPrevious ? -1 : 1;
      }

      return left.name.toLowerCase().compareTo(right.name.toLowerCase());
    });
    return sorted;
  }
}
