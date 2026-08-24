import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter/services.dart';
import 'package:go_router/go_router.dart';
import 'package:shared_preferences/shared_preferences.dart';

import '../../core/theme/trucknav_theme.dart';
import '../../core/theme/system_ui_overlay.dart';
import 'native_navigation_map.dart';
import '../session/session_controller.dart';
import '../session/session_state.dart';

class GpsScreen extends ConsumerStatefulWidget {
  const GpsScreen({super.key});

  @override
  ConsumerState<GpsScreen> createState() => _GpsScreenState();
}

class _GpsScreenState extends ConsumerState<GpsScreen> {
  static const Duration _controlsVisibleDuration = Duration(seconds: 3);
  static const String _darkMapPreferenceKey = 'trucknav.gps.darkMap';

  Timer? _hideTimer;
  bool _controlsVisible = true;
  bool _manualZoomOverride = false;
  double _zoom = 1;
  double _pinchStartZoom = 1;
  Offset _cameraOffset = Offset.zero;
  Offset _panStartOffset = Offset.zero;
  Offset _panStartFocalPoint = Offset.zero;
  String? _activePoiType;
  bool _darkMap = true;
  NavigationViewMode _viewMode = NavigationViewMode.headingUp;

  @override
  void initState() {
    super.initState();
    _restartTimer();
    unawaited(_loadMapAppearance());
  }

  @override
  void dispose() {
    _hideTimer?.cancel();
    super.dispose();
  }

  void _setManualZoom(double zoom, {Offset cameraOffset = Offset.zero}) {
    final nextZoom = NavigationZoomPolicy.clamp(zoom);
    if (nextZoom == _zoom && cameraOffset == _cameraOffset) {
      return;
    }
    setState(() {
      _zoom = nextZoom;
      _manualZoomOverride = true;
      _cameraOffset = cameraOffset;
      if (_viewMode == NavigationViewMode.overview) {
        _viewMode = NavigationViewMode.headingUp;
      }
    });
  }

  void _recenter() {
    setState(() {
      _zoom = 1;
      _manualZoomOverride = false;
      _cameraOffset = Offset.zero;
      _viewMode = NavigationViewMode.headingUp;
    });
    _showControls();
  }

  void _toggleOverview() {
    if (_viewMode == NavigationViewMode.overview) {
      _recenter();
      return;
    }
    setState(() {
      _viewMode = NavigationViewMode.overview;
      _manualZoomOverride = false;
      _cameraOffset = Offset.zero;
    });
    _showControls();
  }

  void _selectPoiType(String? type) {
    setState(() => _activePoiType = _activePoiType == type ? null : type);
    _showControls();
  }

  Future<void> _loadMapAppearance() async {
    final preferences = await SharedPreferences.getInstance();
    final darkMap = preferences.getBool(_darkMapPreferenceKey) ?? true;
    if (mounted && darkMap != _darkMap) {
      setState(() => _darkMap = darkMap);
    }
  }

  Future<void> _toggleMapAppearance() async {
    final darkMap = !_darkMap;
    setState(() => _darkMap = darkMap);
    final preferences = await SharedPreferences.getInstance();
    await preferences.setBool(_darkMapPreferenceKey, darkMap);
    _showControls();
  }

  void _startScale(ScaleStartDetails details) {
    _pinchStartZoom = _zoom;
    _panStartOffset = _cameraOffset;
    _panStartFocalPoint = details.localFocalPoint;
    _showControls();
  }

  void _updateScale(ScaleUpdateDetails details, Size viewportSize) {
    if (details.pointerCount < 2) {
      final drag = details.localFocalPoint - _panStartFocalPoint;
      _setManualZoom(_zoom, cameraOffset: _panStartOffset + drag);
      return;
    }
    final nextZoom = NavigationZoomPolicy.fromGesture(
      _pinchStartZoom,
      details.scale,
    );
    final ratio = nextZoom / _pinchStartZoom;
    final anchor = Offset(
      viewportSize.width / 2,
      viewportSize.height * NavigationCameraTransform.followTruckHeightFraction,
    );
    final focalOffset =
        (details.localFocalPoint - anchor) * (1 - ratio).clamp(-1.5, 1.5);
    _setManualZoom(nextZoom, cameraOffset: _panStartOffset + focalOffset);
  }

  void _endScale(ScaleEndDetails details) {
    _showControls();
  }

  void _restartTimer() {
    _hideTimer?.cancel();
    _hideTimer = Timer(_controlsVisibleDuration, () {
      if (mounted) {
        setState(() => _controlsVisible = false);
      }
    });
  }

  void _showControls() {
    setState(() => _controlsVisible = true);
    _restartTimer();
  }

  Future<void> _openQuickMenu(BuildContext context) async {
    await showModalBottomSheet<void>(
      context: context,
      backgroundColor: TruckNavPalette.surface,
      builder: (context) {
        return SafeArea(
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: <Widget>[
              ListTile(
                key: const Key('gps-map-theme-toggle'),
                leading: Icon(
                  _darkMap ? Icons.light_mode_rounded : Icons.dark_mode_rounded,
                ),
                title: Text(_darkMap ? 'Use light map' : 'Use dark map'),
                onTap: () {
                  Navigator.of(context).pop();
                  unawaited(_toggleMapAppearance());
                },
              ),
              ListTile(
                leading: const Icon(Icons.grid_view_rounded),
                title: const Text('Game selection'),
                onTap: () {
                  ref
                      .read(sessionControllerProvider.notifier)
                      .returnToGameSelection();
                  Navigator.of(context).pop();
                  this.context.go('/connector');
                },
              ),
              ListTile(
                leading: const Icon(Icons.tune_rounded),
                title: const Text('Settings'),
                onTap: () {
                  Navigator.of(context).pop();
                  this.context.go('/settings');
                },
              ),
              ListTile(
                leading: const Icon(Icons.link_off_rounded),
                title: const Text('Disconnect'),
                onTap: () async {
                  Navigator.of(context).pop();
                  await ref
                      .read(sessionControllerProvider.notifier)
                      .disconnect();
                  if (!mounted) {
                    return;
                  }
                  this.context.go('/');
                },
              ),
            ],
          ),
        );
      },
    );
  }

  @override
  Widget build(BuildContext context) {
    ref.listen<SessionNotice?>(
      sessionControllerProvider.select((state) => state.notice),
      (previous, next) {
        if (next == null || !mounted) {
          return;
        }

        ref.read(sessionControllerProvider.notifier).clearNotice();
      },
    );

    final session = ref.watch(sessionControllerProvider);
    final controller = ref.read(sessionControllerProvider.notifier);
    final selectedGameMissing =
        session.selectedGame != TruckGame.none &&
        !session.supportedGames.isRunning(session.selectedGame);

    final mapBrightness = _darkMap ? Brightness.dark : Brightness.light;
    final mapBackground = _darkMap
        ? NavigationMapStyle.background
        : NavigationMapStyle.lightBackground;
    return AnnotatedRegion<SystemUiOverlayStyle>(
      key: const Key('gps-system-ui-style'),
      value: TruckNavSystemUi.forBrightness(
        mapBrightness,
        navigationBarColor: mapBackground,
      ),
      child: Scaffold(
        backgroundColor: mapBackground,
        body: GestureDetector(
          behavior: HitTestBehavior.opaque,
          onTap: _showControls,
          child: Stack(
            fit: StackFit.expand,
            children: <Widget>[
              ColoredBox(
                color: _darkMap
                    ? NavigationMapStyle.background
                    : NavigationMapStyle.lightBackground,
              ),
              SafeArea(
                child: Padding(
                  padding: EdgeInsets.zero,
                  child: Column(
                    children: <Widget>[
                      if (selectedGameMissing) ...<Widget>[
                        _GameNotDetectedBanner(
                          game: session.selectedGame,
                          connectorName:
                              session.selectedConnector?.name ?? 'ROCK',
                          onCheckAgain: controller.requestGameStateRefresh,
                          onChooseAnotherGame: () {
                            controller.returnToGameSelection();
                            context.go('/connector');
                          },
                        ),
                        const SizedBox(height: 6),
                      ],
                      Expanded(
                        child: LayoutBuilder(
                          builder: (context, constraints) {
                            final viewportSize = Size(
                              constraints.maxWidth,
                              constraints.maxHeight,
                            );
                            return GestureDetector(
                              key: const Key('native-gps-gesture'),
                              behavior: HitTestBehavior.opaque,
                              onTap: _showControls,
                              onDoubleTap: () {
                                if (_zoom < NavigationZoomPolicy.maximum) {
                                  _setManualZoom(
                                    _zoom * 1.3,
                                    cameraOffset: _cameraOffset,
                                  );
                                }
                                _showControls();
                              },
                              onScaleStart: _startScale,
                              onScaleUpdate: (details) =>
                                  _updateScale(details, viewportSize),
                              onScaleEnd: _endScale,
                              child: SmoothedNativeNavigationMap(
                                telemetry: session.telemetry,
                                route: session.route,
                                progress: session.routeProgress,
                                zoom: _zoom,
                                viewMode: _viewMode,
                                manualZoomOverride: _manualZoomOverride,
                                visualMapConnection:
                                    session.visualMapConnection,
                                cameraOffset: _cameraOffset,
                                visiblePoiTypes: _activePoiType == null
                                    ? null
                                    : <String>{_activePoiType!},
                                darkMap: _darkMap,
                              ),
                            );
                          },
                        ),
                      ),
                    ],
                  ),
                ),
              ),
              SafeArea(
                child: AnimatedSwitcher(
                  duration: const Duration(milliseconds: 220),
                  child: _controlsVisible
                      ? Align(
                          key: const ValueKey<String>('gps-controls'),
                          alignment: Alignment.centerRight,
                          child: Padding(
                            padding: const EdgeInsets.only(
                              right: 10,
                              bottom: 72,
                            ),
                            child: _ReferenceControlRail(
                              zoomInEnabled:
                                  _zoom < NavigationZoomPolicy.maximum,
                              recenterActive:
                                  _manualZoomOverride ||
                                  _viewMode == NavigationViewMode.overview,
                              onZoomIn: () {
                                _setManualZoom(
                                  _zoom * 1.3,
                                  cameraOffset: _cameraOffset,
                                );
                                _showControls();
                              },
                              onZoomOut: () {
                                _setManualZoom(
                                  _zoom / 1.3,
                                  cameraOffset: _cameraOffset,
                                );
                                _showControls();
                              },
                              onRecenter: _recenter,
                            ),
                          ),
                        )
                      : const SizedBox.shrink(),
                ),
              ),
              SafeArea(
                child: Align(
                  alignment: Alignment.bottomCenter,
                  child: Padding(
                    padding: const EdgeInsets.fromLTRB(12, 0, 12, 10),
                    child: RepaintBoundary(
                      child: _MapDock(
                        activePoiType: _activePoiType,
                        overviewActive:
                            _viewMode == NavigationViewMode.overview,
                        onPoiSelected: _selectPoiType,
                        onOverviewPressed: _toggleOverview,
                        onMenuPressed: () => _openQuickMenu(context),
                      ),
                    ),
                  ),
                ),
              ),
              if (session.weakConnection)
                const Positioned(
                  left: 18,
                  right: 18,
                  bottom: 142,
                  child: _WeakConnectionBanner(),
                ),
              if (session.connectionStage == ConnectionStage.reconnecting)
                const _GpsReconnectOverlay(),
            ],
          ),
        ),
      ),
    );
  }
}

class _ReferenceControlRail extends StatelessWidget {
  const _ReferenceControlRail({
    required this.zoomInEnabled,
    required this.recenterActive,
    required this.onZoomIn,
    required this.onZoomOut,
    required this.onRecenter,
  });

  final bool zoomInEnabled;
  final bool recenterActive;
  final VoidCallback onZoomIn;
  final VoidCallback onZoomOut;
  final VoidCallback onRecenter;

  @override
  Widget build(BuildContext context) {
    return Container(
      key: const Key('gps-control-rail'),
      width: 46,
      decoration: BoxDecoration(
        color: const Color(0xF208151F),
        borderRadius: BorderRadius.circular(10),
        border: Border.all(color: const Color(0xFF40515D), width: 1.2),
        boxShadow: const <BoxShadow>[
          BoxShadow(color: Color(0x99000000), blurRadius: 12),
        ],
      ),
      clipBehavior: Clip.antiAlias,
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: <Widget>[
          _RailButton(
            controlKey: const Key('gps-zoom-in'),
            tooltip: 'Zoom in',
            icon: Icons.add_rounded,
            onPressed: zoomInEnabled ? onZoomIn : null,
          ),
          const Divider(height: 1, color: Color(0xFF3A4954)),
          _RailButton(
            controlKey: const Key('gps-zoom-out'),
            tooltip: 'Zoom out',
            icon: Icons.remove_rounded,
            onPressed: onZoomOut,
          ),
          const Divider(height: 1, color: Color(0xFF3A4954)),
          _RailButton(
            controlKey: const Key('gps-recenter'),
            tooltip: 'Recenter on truck',
            icon: recenterActive
                ? Icons.gps_fixed_rounded
                : Icons.my_location_rounded,
            onPressed: onRecenter,
          ),
        ],
      ),
    );
  }
}

class _RailButton extends StatelessWidget {
  const _RailButton({
    required this.controlKey,
    required this.tooltip,
    required this.icon,
    required this.onPressed,
  });

  final Key controlKey;
  final String tooltip;
  final IconData icon;
  final VoidCallback? onPressed;

  @override
  Widget build(BuildContext context) {
    return IconButton(
      key: controlKey,
      onPressed: onPressed,
      tooltip: tooltip,
      icon: Icon(icon, size: 27),
      color: const Color(0xFFF0F3F4),
      disabledColor: const Color(0xFF5A6770),
      style: IconButton.styleFrom(
        fixedSize: const Size(46, 50),
        shape: const RoundedRectangleBorder(),
      ),
    );
  }
}

class _MapDock extends StatelessWidget {
  const _MapDock({
    required this.activePoiType,
    required this.overviewActive,
    required this.onPoiSelected,
    required this.onOverviewPressed,
    required this.onMenuPressed,
  });

  final String? activePoiType;
  final bool overviewActive;
  final ValueChanged<String?> onPoiSelected;
  final VoidCallback onOverviewPressed;
  final VoidCallback onMenuPressed;

  static const List<(String, String, IconData)> _categories =
      <(String, String, IconData)>[
        ('Fuel', 'Fuel', Icons.local_gas_station_rounded),
        ('Rest', 'Sleep', Icons.hotel_rounded),
        ('Service', 'Service', Icons.build_rounded),
        ('Garage', 'Garage', Icons.garage_rounded),
        ('Dealer', 'Dealer', Icons.local_shipping_rounded),
      ];

  @override
  Widget build(BuildContext context) {
    return ConstrainedBox(
      constraints: const BoxConstraints(maxWidth: 560),
      child: Container(
        key: const Key('gps-map-dock'),
        width: double.infinity,
        padding: const EdgeInsets.fromLTRB(5, 5, 4, 2),
        decoration: BoxDecoration(
          color: const Color(0xF208151F),
          borderRadius: BorderRadius.circular(10),
          border: Border.all(color: const Color(0xFF40515D), width: 1.2),
          boxShadow: const <BoxShadow>[
            BoxShadow(
              color: Color(0x99000000),
              blurRadius: 14,
              offset: Offset(0, 5),
            ),
          ],
        ),
        child: Row(
          children: <Widget>[
            Expanded(
              child: _MapDockButton(
                buttonKey: const Key('gps-overview'),
                label: 'Route',
                icon: Icons.route_rounded,
                selected: overviewActive || activePoiType == null,
                onPressed: onOverviewPressed,
              ),
            ),
            Container(
              width: 1,
              height: 35,
              margin: const EdgeInsets.symmetric(horizontal: 2),
              color: const Color(0xFF2A3943),
            ),
            for (final category in _categories)
              Expanded(
                child: _MapDockButton(
                  buttonKey: Key('gps-poi-${category.$1.toLowerCase()}'),
                  label: category.$2,
                  icon: category.$3,
                  selected: activePoiType == category.$1,
                  onPressed: () => onPoiSelected(category.$1),
                ),
              ),
            SizedBox(
              width: 32,
              child: IconButton(
                key: const Key('gps-menu'),
                onPressed: onMenuPressed,
                tooltip: 'Menu',
                padding: EdgeInsets.zero,
                icon: const Icon(
                  Icons.more_vert_rounded,
                  color: Color(0xFFB8C2C8),
                  size: 24,
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _MapDockButton extends StatelessWidget {
  const _MapDockButton({
    required this.buttonKey,
    required this.label,
    required this.icon,
    required this.selected,
    required this.onPressed,
  });

  final Key buttonKey;
  final String label;
  final IconData icon;
  final bool selected;
  final VoidCallback onPressed;

  @override
  Widget build(BuildContext context) {
    final color = selected
        ? NavigationMapStyle.upcomingRoute
        : const Color(0xFFB6C6CE);
    return InkWell(
      key: buttonKey,
      onTap: onPressed,
      borderRadius: BorderRadius.circular(7),
      child: Padding(
        padding: const EdgeInsets.fromLTRB(2, 3, 2, 0),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: <Widget>[
            Icon(icon, size: 21, color: color),
            const SizedBox(height: 3),
            Text(
              label,
              maxLines: 1,
              overflow: TextOverflow.fade,
              style: TextStyle(
                color: color,
                fontSize: 9,
                fontWeight: FontWeight.w700,
              ),
            ),
            const SizedBox(height: 4),
            Container(
              height: 2.5,
              width: 38,
              decoration: BoxDecoration(
                color: selected
                    ? NavigationMapStyle.upcomingRoute
                    : Colors.transparent,
                borderRadius: BorderRadius.circular(2),
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _WeakConnectionBanner extends StatelessWidget {
  const _WeakConnectionBanner();

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 8),
      decoration: BoxDecoration(
        color: TruckNavPalette.surface.withValues(alpha: 0.95),
        borderRadius: BorderRadius.circular(5),
        border: Border.all(color: TruckNavPalette.border),
      ),
      child: Text(
        'Weak connection. TruckNav is still receiving pong responses, but the LAN session is degraded.',
        textAlign: TextAlign.center,
        style: Theme.of(
          context,
        ).textTheme.bodyMedium?.copyWith(color: TruckNavPalette.textPrimary),
      ),
    );
  }
}

class _GameNotDetectedBanner extends StatelessWidget {
  const _GameNotDetectedBanner({
    required this.game,
    required this.connectorName,
    required this.onCheckAgain,
    required this.onChooseAnotherGame,
  });

  final TruckGame game;
  final String connectorName;
  final Future<void> Function() onCheckAgain;
  final VoidCallback onChooseAnotherGame;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(18),
      decoration: BoxDecoration(
        color: TruckNavPalette.surface.withValues(alpha: 0.95),
        borderRadius: BorderRadius.circular(22),
        border: Border.all(color: TruckNavPalette.border),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          Text(
            'Game not detected',
            style: Theme.of(context).textTheme.titleMedium?.copyWith(
              color: TruckNavPalette.accent,
              fontWeight: FontWeight.w800,
            ),
          ),
          const SizedBox(height: 8),
          Text(
            'Start ${gameLabel(game)} on $connectorName to continue.',
            style: Theme.of(context).textTheme.bodyMedium?.copyWith(
              color: TruckNavPalette.textSecondary,
              height: 1.45,
            ),
          ),
          const SizedBox(height: 14),
          Wrap(
            spacing: 10,
            runSpacing: 10,
            children: <Widget>[
              FilledButton(
                onPressed: () {
                  onCheckAgain();
                },
                style: FilledButton.styleFrom(
                  backgroundColor: TruckNavPalette.accent,
                  foregroundColor: TruckNavPalette.background,
                ),
                child: const Text('Check Again'),
              ),
              OutlinedButton(
                onPressed: onChooseAnotherGame,
                style: OutlinedButton.styleFrom(
                  side: const BorderSide(color: TruckNavPalette.border),
                  foregroundColor: TruckNavPalette.textPrimary,
                ),
                child: const Text('Choose Another Game'),
              ),
            ],
          ),
        ],
      ),
    );
  }
}

class _GpsReconnectOverlay extends StatelessWidget {
  const _GpsReconnectOverlay();

  @override
  Widget build(BuildContext context) {
    return ColoredBox(
      color: Colors.black54,
      child: Center(
        child: Container(
          margin: const EdgeInsets.symmetric(horizontal: 30),
          padding: const EdgeInsets.all(24),
          decoration: BoxDecoration(
            color: TruckNavPalette.surface,
            borderRadius: BorderRadius.circular(28),
            border: Border.all(color: TruckNavPalette.border),
          ),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: const <Widget>[
              CircularProgressIndicator(),
              SizedBox(height: 18),
              Text(
                'Reconnecting',
                style: TextStyle(fontSize: 22, fontWeight: FontWeight.w800),
              ),
              SizedBox(height: 8),
              Text(
                'TruckNav is waiting for the previous Connector to reappear on the LAN before restoring the control session.',
                textAlign: TextAlign.center,
              ),
            ],
          ),
        ),
      ),
    );
  }
}
