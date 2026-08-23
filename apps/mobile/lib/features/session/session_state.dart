enum ConnectionStage {
  initializing,
  namingDevice,
  localNetworkAccessRequired,
  discovering,
  noPcFound,
  connecting,
  connectionFailed,
  versionMismatch,
  gameSelection,
  waitingForStream,
  streaming,
  reconnecting,
}

enum TruckGame { none, euroTruckSimulator2, americanTruckSimulator }

enum NavigationCommandKind { zoomIn, zoomOut, cycleNavigationView }

enum SessionNoticeKind { info, success, warning }

class SessionNotice {
  const SessionNotice({required this.kind, required this.message});

  final SessionNoticeKind kind;
  final String message;
}

class ConnectorSummary {
  const ConnectorSummary({
    required this.id,
    required this.name,
    required this.host,
    required this.addresses,
    required this.port,
    required this.protocolVersion,
    required this.appVersion,
    required this.isPrevious,
    required this.lastSeen,
    this.platformLabel = 'Windows PC',
    this.networkLabel = 'Same local network',
  });

  final String id;
  final String name;
  final String host;
  final List<String> addresses;
  final int port;
  final int protocolVersion;
  final String appVersion;
  final bool isPrevious;
  final DateTime lastSeen;
  final String platformLabel;
  final String networkLabel;

  ConnectorSummary copyWith({
    String? id,
    String? name,
    String? host,
    List<String>? addresses,
    int? port,
    int? protocolVersion,
    String? appVersion,
    bool? isPrevious,
    DateTime? lastSeen,
    String? platformLabel,
    String? networkLabel,
  }) {
    return ConnectorSummary(
      id: id ?? this.id,
      name: name ?? this.name,
      host: host ?? this.host,
      addresses: addresses ?? this.addresses,
      port: port ?? this.port,
      protocolVersion: protocolVersion ?? this.protocolVersion,
      appVersion: appVersion ?? this.appVersion,
      isPrevious: isPrevious ?? this.isPrevious,
      lastSeen: lastSeen ?? this.lastSeen,
      platformLabel: platformLabel ?? this.platformLabel,
      networkLabel: networkLabel ?? this.networkLabel,
    );
  }
}

class SessionState {
  const SessionState({
    required this.deviceId,
    required this.deviceName,
    required this.connectionStage,
    required this.availableConnectors,
    required this.autoReconnect,
    required this.autoOpenRunningGame,
    required this.runningGame,
    required this.selectedGame,
    required this.weakConnection,
    required this.hasCompletedInitialScan,
    this.selectedConnector,
    this.lastConnectedDeviceId,
    this.connectionError,
    this.notice,
    this.lastCommandLabel,
  });

  static const Object _unset = Object();

  factory SessionState.initial() {
    return const SessionState(
      deviceId: '',
      deviceName: 'HEN',
      connectionStage: ConnectionStage.initializing,
      availableConnectors: <ConnectorSummary>[],
      selectedConnector: null,
      autoReconnect: true,
      autoOpenRunningGame: false,
      runningGame: TruckGame.none,
      selectedGame: TruckGame.none,
      weakConnection: false,
      hasCompletedInitialScan: false,
      lastConnectedDeviceId: null,
      connectionError: null,
      notice: null,
      lastCommandLabel: null,
    );
  }

  final String deviceId;
  final String deviceName;
  final ConnectionStage connectionStage;
  final List<ConnectorSummary> availableConnectors;
  final ConnectorSummary? selectedConnector;
  final bool autoReconnect;
  final bool autoOpenRunningGame;
  final TruckGame runningGame;
  final TruckGame selectedGame;
  final bool weakConnection;
  final bool hasCompletedInitialScan;
  final String? lastConnectedDeviceId;
  final String? connectionError;
  final SessionNotice? notice;
  final String? lastCommandLabel;

  SessionState copyWith({
    String? deviceId,
    String? deviceName,
    ConnectionStage? connectionStage,
    List<ConnectorSummary>? availableConnectors,
    Object? selectedConnector = _unset,
    bool? autoReconnect,
    bool? autoOpenRunningGame,
    TruckGame? runningGame,
    TruckGame? selectedGame,
    bool? weakConnection,
    bool? hasCompletedInitialScan,
    Object? lastConnectedDeviceId = _unset,
    Object? connectionError = _unset,
    Object? notice = _unset,
    Object? lastCommandLabel = _unset,
  }) {
    return SessionState(
      deviceId: deviceId ?? this.deviceId,
      deviceName: deviceName ?? this.deviceName,
      connectionStage: connectionStage ?? this.connectionStage,
      availableConnectors: availableConnectors ?? this.availableConnectors,
      selectedConnector: identical(selectedConnector, _unset)
          ? this.selectedConnector
          : selectedConnector as ConnectorSummary?,
      autoReconnect: autoReconnect ?? this.autoReconnect,
      autoOpenRunningGame: autoOpenRunningGame ?? this.autoOpenRunningGame,
      runningGame: runningGame ?? this.runningGame,
      selectedGame: selectedGame ?? this.selectedGame,
      weakConnection: weakConnection ?? this.weakConnection,
      hasCompletedInitialScan:
          hasCompletedInitialScan ?? this.hasCompletedInitialScan,
      lastConnectedDeviceId: identical(lastConnectedDeviceId, _unset)
          ? this.lastConnectedDeviceId
          : lastConnectedDeviceId as String?,
      connectionError: identical(connectionError, _unset)
          ? this.connectionError
          : connectionError as String?,
      notice: identical(notice, _unset) ? this.notice : notice as SessionNotice?,
      lastCommandLabel: identical(lastCommandLabel, _unset)
          ? this.lastCommandLabel
          : lastCommandLabel as String?,
    );
  }
}

String gameLabel(TruckGame game) => switch (game) {
      TruckGame.none => 'No supported game detected',
      TruckGame.euroTruckSimulator2 => 'Euro Truck Simulator 2',
      TruckGame.americanTruckSimulator => 'American Truck Simulator',
    };

String commandLabel(NavigationCommandKind command) => switch (command) {
      NavigationCommandKind.zoomIn => 'Zoom in',
      NavigationCommandKind.zoomOut => 'Zoom out',
      NavigationCommandKind.cycleNavigationView => 'Change navigation view',
    };
