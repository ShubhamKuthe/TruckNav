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
  waitingForNavigation,
  navigating,
  reconnecting,
}

enum TruckGame { none, euroTruckSimulator2, americanTruckSimulator }

enum SessionNoticeKind { info, success, warning }

class VisualMapConnection {
  const VisualMapConnection({
    required this.host,
    required this.port,
    required this.accessToken,
  });

  final String host;
  final int port;
  final String accessToken;

  Uri uri(String path) =>
      Uri(scheme: 'http', host: host, port: port, path: path);
}

class NativeTelemetryState {
  const NativeTelemetryState({
    required this.game,
    required this.connected,
    required this.paused,
    required this.x,
    required this.z,
    required this.elevation,
    required this.headingDegrees,
    required this.speedKph,
    required this.speedLimitKph,
    required this.onJob,
    required this.destinationCity,
    required this.destinationCompany,
    required this.gameNavigationDistanceMeters,
    required this.gameNavigationTimeSeconds,
    required this.navigationStatus,
    required this.statusMessage,
    required this.timestampUtc,
  });

  const NativeTelemetryState.disconnected()
    : game = TruckGame.none,
      connected = false,
      paused = false,
      x = 0,
      z = 0,
      elevation = 0,
      headingDegrees = 0,
      speedKph = 0,
      speedLimitKph = 0,
      onJob = false,
      destinationCity = null,
      destinationCompany = null,
      gameNavigationDistanceMeters = 0,
      gameNavigationTimeSeconds = 0,
      navigationStatus = 'WaitingForTelemetry',
      statusMessage = 'Start ETS2 or ATS to begin.',
      timestampUtc = null;

  final TruckGame game;
  final bool connected;
  final bool paused;
  final double x;
  final double z;
  final double elevation;
  final double headingDegrees;
  final double speedKph;
  final double speedLimitKph;
  final bool onJob;
  final String? destinationCity;
  final String? destinationCompany;
  final double gameNavigationDistanceMeters;
  final double gameNavigationTimeSeconds;
  final String navigationStatus;
  final String statusMessage;
  final DateTime? timestampUtc;
}

class NativeRoutePoint {
  const NativeRoutePoint(this.x, this.z);

  final double x;
  final double z;
}

class NativeRouteStep {
  const NativeRouteStep({
    required this.maneuver,
    required this.instruction,
    required this.pointIndex,
    required this.distanceFromStartMeters,
  });

  final String maneuver;
  final String instruction;
  final int pointIndex;
  final double distanceFromStartMeters;
}

class NativeContextRoad {
  const NativeContextRoad({
    required this.fromX,
    required this.fromZ,
    required this.toX,
    required this.toZ,
    required this.isMajor,
    required this.isFerry,
  });

  final double fromX;
  final double fromZ;
  final double toX;
  final double toZ;
  final bool isMajor;
  final bool isFerry;
}

class NativeMapPoint {
  const NativeMapPoint({
    required this.type,
    required this.x,
    required this.z,
    required this.label,
    required this.isDestination,
  });

  final String type;
  final double x;
  final double z;
  final String? label;
  final bool isDestination;
}

class NativeMapLabel {
  const NativeMapLabel({
    required this.text,
    required this.x,
    required this.z,
    required this.isDestination,
  });

  final String text;
  final double x;
  final double z;
  final bool isDestination;
}

class NativeRouteState {
  const NativeRouteState({
    required this.revision,
    required this.available,
    required this.routeId,
    required this.game,
    required this.destination,
    required this.points,
    required this.steps,
    required this.contextRoads,
    required this.mapPoints,
    required this.mapLabels,
    required this.totalDistanceMeters,
    required this.estimatedTimeSeconds,
    required this.calculatedUtc,
    required this.isReroute,
  });

  const NativeRouteState.empty()
    : revision = 0,
      available = false,
      routeId = null,
      game = TruckGame.none,
      destination = null,
      points = const <NativeRoutePoint>[],
      steps = const <NativeRouteStep>[],
      contextRoads = const <NativeContextRoad>[],
      mapPoints = const <NativeMapPoint>[],
      mapLabels = const <NativeMapLabel>[],
      totalDistanceMeters = 0,
      estimatedTimeSeconds = 0,
      calculatedUtc = null,
      isReroute = false;

  final int revision;
  final bool available;
  final String? routeId;
  final TruckGame game;
  final String? destination;
  final List<NativeRoutePoint> points;
  final List<NativeRouteStep> steps;
  final List<NativeContextRoad> contextRoads;
  final List<NativeMapPoint> mapPoints;
  final List<NativeMapLabel> mapLabels;
  final double totalDistanceMeters;
  final double estimatedTimeSeconds;
  final DateTime? calculatedUtc;
  final bool isReroute;
}

class NativeRouteProgress {
  const NativeRouteProgress({
    required this.routeId,
    required this.segmentIndex,
    required this.projectedX,
    required this.projectedZ,
    required this.distanceFromRouteMeters,
    required this.remainingDistanceMeters,
    required this.remainingTimeSeconds,
    required this.nextManeuver,
    required this.nextInstruction,
    required this.distanceToNextManeuverMeters,
    required this.isOffRoute,
    required this.teleportDetected,
    required this.timestampUtc,
  });

  final String routeId;
  final int segmentIndex;
  final double projectedX;
  final double projectedZ;
  final double distanceFromRouteMeters;
  final double remainingDistanceMeters;
  final double remainingTimeSeconds;
  final String nextManeuver;
  final String nextInstruction;
  final double distanceToNextManeuverMeters;
  final bool isOffRoute;
  final bool teleportDetected;
  final DateTime? timestampUtc;
}

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

class SupportedGameState {
  const SupportedGameState({
    required this.ets2Running,
    required this.atsRunning,
  });

  const SupportedGameState.none() : ets2Running = false, atsRunning = false;

  final bool ets2Running;
  final bool atsRunning;

  bool get hasAnyRunningGame => ets2Running || atsRunning;

  bool get hasMultipleRunningGames => ets2Running && atsRunning;

  List<TruckGame> get runningGames => <TruckGame>[
    if (ets2Running) TruckGame.euroTruckSimulator2,
    if (atsRunning) TruckGame.americanTruckSimulator,
  ];

  TruckGame get singleRunningGameOrNone => switch (runningGames) {
    [final TruckGame game] => game,
    _ => TruckGame.none,
  };

  bool isRunning(TruckGame game) => switch (game) {
    TruckGame.euroTruckSimulator2 => ets2Running,
    TruckGame.americanTruckSimulator => atsRunning,
    TruckGame.none => false,
  };

  SupportedGameState copyWith({bool? ets2Running, bool? atsRunning}) {
    return SupportedGameState(
      ets2Running: ets2Running ?? this.ets2Running,
      atsRunning: atsRunning ?? this.atsRunning,
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
    required this.supportedGames,
    required this.runningGame,
    required this.selectedGame,
    required this.weakConnection,
    required this.hasCompletedInitialScan,
    required this.telemetry,
    required this.route,
    this.routeProgress,
    this.visualMapConnection,
    this.selectedConnector,
    this.lastConnectedDeviceId,
    this.connectionError,
    this.notice,
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
      supportedGames: SupportedGameState.none(),
      runningGame: TruckGame.none,
      selectedGame: TruckGame.none,
      weakConnection: false,
      hasCompletedInitialScan: false,
      telemetry: NativeTelemetryState.disconnected(),
      route: NativeRouteState.empty(),
      lastConnectedDeviceId: null,
      connectionError: null,
      notice: null,
    );
  }

  final String deviceId;
  final String deviceName;
  final ConnectionStage connectionStage;
  final List<ConnectorSummary> availableConnectors;
  final ConnectorSummary? selectedConnector;
  final bool autoReconnect;
  final bool autoOpenRunningGame;
  final SupportedGameState supportedGames;
  final TruckGame runningGame;
  final TruckGame selectedGame;
  final bool weakConnection;
  final bool hasCompletedInitialScan;
  final NativeTelemetryState telemetry;
  final NativeRouteState route;
  final NativeRouteProgress? routeProgress;
  final VisualMapConnection? visualMapConnection;
  final String? lastConnectedDeviceId;
  final String? connectionError;
  final SessionNotice? notice;

  SessionState copyWith({
    String? deviceId,
    String? deviceName,
    ConnectionStage? connectionStage,
    List<ConnectorSummary>? availableConnectors,
    Object? selectedConnector = _unset,
    bool? autoReconnect,
    bool? autoOpenRunningGame,
    SupportedGameState? supportedGames,
    TruckGame? runningGame,
    TruckGame? selectedGame,
    bool? weakConnection,
    bool? hasCompletedInitialScan,
    NativeTelemetryState? telemetry,
    NativeRouteState? route,
    Object? routeProgress = _unset,
    Object? visualMapConnection = _unset,
    Object? lastConnectedDeviceId = _unset,
    Object? connectionError = _unset,
    Object? notice = _unset,
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
      supportedGames: supportedGames ?? this.supportedGames,
      runningGame: runningGame ?? this.runningGame,
      selectedGame: selectedGame ?? this.selectedGame,
      weakConnection: weakConnection ?? this.weakConnection,
      hasCompletedInitialScan:
          hasCompletedInitialScan ?? this.hasCompletedInitialScan,
      telemetry: telemetry ?? this.telemetry,
      route: route ?? this.route,
      routeProgress: identical(routeProgress, _unset)
          ? this.routeProgress
          : routeProgress as NativeRouteProgress?,
      visualMapConnection: identical(visualMapConnection, _unset)
          ? this.visualMapConnection
          : visualMapConnection as VisualMapConnection?,
      lastConnectedDeviceId: identical(lastConnectedDeviceId, _unset)
          ? this.lastConnectedDeviceId
          : lastConnectedDeviceId as String?,
      connectionError: identical(connectionError, _unset)
          ? this.connectionError
          : connectionError as String?,
      notice: identical(notice, _unset)
          ? this.notice
          : notice as SessionNotice?,
    );
  }
}

String gameLabel(TruckGame game) => switch (game) {
  TruckGame.none => 'No supported game detected',
  TruckGame.euroTruckSimulator2 => 'Euro Truck Simulator 2',
  TruckGame.americanTruckSimulator => 'American Truck Simulator',
};
