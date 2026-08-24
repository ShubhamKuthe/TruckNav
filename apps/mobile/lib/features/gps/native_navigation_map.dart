import 'dart:math' as math;

import 'package:flutter/material.dart';

import '../../core/theme/trucknav_theme.dart';
import '../session/session_state.dart';
import 'navigation_geometry.dart';
import 'visual_map_tiles.dart';

export 'navigation_geometry.dart';

class NavigationPose {
  const NavigationPose({
    required this.x,
    required this.z,
    required this.headingDegrees,
  });

  factory NavigationPose.fromTelemetry(NativeTelemetryState telemetry) =>
      NavigationPose(
        x: telemetry.x,
        z: telemetry.z,
        headingDegrees: telemetry.headingDegrees,
      );

  final double x;
  final double z;
  final double headingDegrees;

  static NavigationPose stabilizeHeading(
    NavigationPose current,
    NavigationPose target,
    double speedKph,
  ) {
    // Ignore tiny heading noise while parked without delaying real turns.
    final delta = NavigationHeading.shortestDelta(
      current.headingDegrees,
      target.headingDegrees,
    ).abs();
    if (speedKph < 1 && delta < 6) {
      return NavigationPose(
        x: target.x,
        z: target.z,
        headingDegrees: current.headingDegrees,
      );
    }
    return target;
  }

  static NavigationPose interpolate(
    NavigationPose from,
    NavigationPose to,
    double progress, {
    double teleportThresholdMeters = 2000,
  }) {
    final distance = math.sqrt(
      math.pow(to.x - from.x, 2) + math.pow(to.z - from.z, 2),
    );
    if (distance >= teleportThresholdMeters) {
      return to;
    }

    final amount = progress.clamp(0.0, 1.0);
    final headingDelta = NavigationHeading.shortestDelta(
      from.headingDegrees,
      to.headingDegrees,
    );
    final heading = NavigationHeading.normalizeDegrees(
      from.headingDegrees + headingDelta * amount,
    );
    return NavigationPose(
      x: from.x + (to.x - from.x) * amount,
      z: from.z + (to.z - from.z) * amount,
      headingDegrees: heading,
    );
  }
}

class SmoothedNativeNavigationMap extends StatefulWidget {
  const SmoothedNativeNavigationMap({
    required this.telemetry,
    required this.route,
    required this.progress,
    required this.zoom,
    required this.viewMode,
    required this.manualZoomOverride,
    this.darkMap = true,
    this.visualMapConnection,
    this.cameraOffset = Offset.zero,
    this.visiblePoiTypes,
    super.key,
  });

  final NativeTelemetryState telemetry;
  final NativeRouteState route;
  final NativeRouteProgress? progress;
  final double zoom;
  final NavigationViewMode viewMode;
  final bool manualZoomOverride;
  final bool darkMap;
  final VisualMapConnection? visualMapConnection;
  final Offset cameraOffset;
  final Set<String>? visiblePoiTypes;

  @override
  State<SmoothedNativeNavigationMap> createState() =>
      _SmoothedNativeNavigationMapState();
}

class _SmoothedNativeNavigationMapState
    extends State<SmoothedNativeNavigationMap>
    with TickerProviderStateMixin {
  late final AnimationController _poseController;
  late final AnimationController _zoomController;
  late NavigationPose _poseFrom;
  late NavigationPose _poseTo;
  late double _zoomFrom;
  late double _zoomTo;

  @override
  void initState() {
    super.initState();
    _poseFrom = NavigationPose.fromTelemetry(widget.telemetry);
    _poseTo = _poseFrom;
    _zoomFrom = _targetZoom(widget);
    _zoomTo = _zoomFrom;
    _poseController = AnimationController(
      vsync: this,
      duration: const Duration(milliseconds: 90),
      value: 1,
    );
    _zoomController = AnimationController(
      vsync: this,
      duration: const Duration(milliseconds: 650),
      value: 1,
    );
  }

  @override
  void didUpdateWidget(covariant SmoothedNativeNavigationMap oldWidget) {
    super.didUpdateWidget(oldWidget);
    final currentPose = NavigationPose.interpolate(
      _poseFrom,
      _poseTo,
      _poseController.value,
    );
    final rawTarget = NavigationPose.fromTelemetry(widget.telemetry);
    final target = NavigationPose.stabilizeHeading(
      currentPose,
      rawTarget,
      widget.telemetry.speedKph,
    );
    _poseFrom = currentPose;
    _poseTo = target;
    final distance = math.sqrt(
      math.pow(_poseTo.x - _poseFrom.x, 2) +
          math.pow(_poseTo.z - _poseFrom.z, 2),
    );
    if (distance >= 2000 || !widget.telemetry.connected) {
      _poseFrom = target;
      _poseController.value = 1;
    } else {
      _poseController.forward(from: 0);
    }

    final targetZoom = _targetZoom(widget);
    final currentZoom = _lerp(_zoomFrom, _zoomTo, _zoomController.value);
    if ((targetZoom - currentZoom).abs() >= 0.025) {
      _zoomFrom = currentZoom;
      _zoomTo = targetZoom;
      _zoomController.forward(from: 0);
    }
  }

  static double _targetZoom(SmoothedNativeNavigationMap widget) {
    final adaptive = widget.manualZoomOverride
        ? 1.0
        : NavigationZoomPolicy.adaptiveFactor(
            speedKph: widget.telemetry.speedKph,
            maneuverDistanceMeters:
                widget.progress?.distanceToNextManeuverMeters,
          );
    return NavigationZoomPolicy.clamp(widget.zoom * adaptive);
  }

  static double _lerp(double from, double to, double amount) =>
      from + (to - from) * Curves.easeOutCubic.transform(amount);

  @override
  void dispose() {
    _poseController.dispose();
    _zoomController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return AnimatedBuilder(
      animation: Listenable.merge(<Listenable>[
        _poseController,
        _zoomController,
      ]),
      builder: (context, child) => NativeNavigationMap(
        telemetry: widget.telemetry,
        route: widget.route,
        progress: widget.progress,
        zoom: _lerp(_zoomFrom, _zoomTo, _zoomController.value),
        viewMode: widget.viewMode,
        cameraOffset: widget.cameraOffset,
        visualMapConnection: widget.visualMapConnection,
        visiblePoiTypes: widget.visiblePoiTypes,
        darkMap: widget.darkMap,
        pose: NavigationPose.interpolate(
          _poseFrom,
          _poseTo,
          _poseController.value,
        ),
      ),
    );
  }
}

class NativeNavigationMap extends StatelessWidget {
  const NativeNavigationMap({
    required this.telemetry,
    required this.route,
    required this.progress,
    required this.zoom,
    required this.viewMode,
    this.darkMap = true,
    this.visualMapConnection,
    this.pose,
    this.cameraOffset = Offset.zero,
    this.visiblePoiTypes,
    super.key,
  });

  final NativeTelemetryState telemetry;
  final NativeRouteState route;
  final NativeRouteProgress? progress;
  final double zoom;
  final NavigationViewMode viewMode;
  final bool darkMap;
  final VisualMapConnection? visualMapConnection;
  final NavigationPose? pose;
  final Offset cameraOffset;
  final Set<String>? visiblePoiTypes;

  @override
  Widget build(BuildContext context) {
    return ColoredBox(
      color: darkMap
          ? NavigationMapStyle.background
          : NavigationMapStyle.lightBackground,
      child: Stack(
        fit: StackFit.expand,
        children: <Widget>[
          RepaintBoundary(
            child: VisualMapTileLayer(
              connection: visualMapConnection,
              telemetry: telemetry,
              route: route,
              zoom: zoom,
              viewMode: viewMode,
              displayX: (pose ?? NavigationPose.fromTelemetry(telemetry)).x,
              displayZ: (pose ?? NavigationPose.fromTelemetry(telemetry)).z,
              displayHeadingDegrees:
                  (pose ?? NavigationPose.fromTelemetry(telemetry))
                      .headingDegrees,
              cameraOffset: cameraOffset,
              visiblePoiTypes: visiblePoiTypes,
              darkMode: darkMap,
            ),
          ),
          RepaintBoundary(
            child: CustomPaint(
              painter: NavigationPainter(
                telemetry: telemetry,
                route: route,
                progress: progress,
                zoom: zoom,
                viewMode: viewMode,
                pose: pose,
                cameraOffset: cameraOffset,
              ),
            ),
          ),
          if (telemetry.connected && route.available)
            _ManeuverHud(progress: progress),
          if (telemetry.connected)
            Positioned(
              left: 10,
              bottom: 86,
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                mainAxisSize: MainAxisSize.min,
                children: <Widget>[
                  if (telemetry.speedLimitKph > 0) ...<Widget>[
                    _SpeedLimitBadge(telemetry: telemetry),
                    const SizedBox(height: 8),
                  ],
                  _SpeedHud(telemetry: telemetry),
                ],
              ),
            ),
          if (telemetry.connected && route.available)
            Positioned(
              right: 10,
              top: 10,
              child: _TripHud(progress: progress, route: route),
            ),
          if (!telemetry.connected) _NavigationStatus(telemetry: telemetry),
          if (telemetry.connected && !route.available)
            Positioned(
              top: 64,
              right: 12,
              width: 272,
              child: _NavigationStatus(telemetry: telemetry, compact: true),
            ),
          if (telemetry.connected && telemetry.paused)
            Positioned(
              left: 12,
              top: route.available ? 164 : 12,
              child: const _StatusPill(
                icon: Icons.pause_circle_filled_rounded,
                label: 'GAME PAUSED',
                color: Color(0xFFFFA629),
              ),
            ),
          if (telemetry.navigationStatus == 'Rerouting')
            const Positioned(
              left: 12,
              bottom: 142,
              child: _StatusPill(
                icon: Icons.alt_route_rounded,
                label: 'Rerouting...',
                color: NavigationMapStyle.directionChevron,
              ),
            )
          else if (progress?.isOffRoute ?? false)
            Positioned(
              left: 12,
              bottom: 142,
              child: _StatusPill(
                icon: Icons.alt_route_rounded,
                label: offRouteStatusLabel(progress!.distanceFromRouteMeters),
                color: NavigationMapStyle.upcomingRoute,
              ),
            ),
        ],
      ),
    );
  }
}

@visibleForTesting
class NavigationPainter extends CustomPainter {
  NavigationPainter({
    required this.telemetry,
    required this.route,
    required this.progress,
    required this.zoom,
    required this.viewMode,
    required this.pose,
    this.cameraOffset = Offset.zero,
  });

  final NativeTelemetryState telemetry;
  final NativeRouteState route;
  final NativeRouteProgress? progress;
  final double zoom;
  final NavigationViewMode viewMode;
  final NavigationPose? pose;
  final Offset cameraOffset;

  @override
  void paint(Canvas canvas, Size size) {
    if (!telemetry.connected) {
      return;
    }

    final displayPose = pose ?? NavigationPose.fromTelemetry(telemetry);
    final hasRoute = route.available && route.points.length >= 2;
    final transform = viewMode == NavigationViewMode.overview && hasRoute
        ? NavigationCameraTransform.overview(
            size: size,
            worldPoints: route.points
                .map((point) => Offset(point.x, point.z))
                .toList(growable: false),
          )
        : NavigationCameraTransform.follow(
            size: size,
            truckX: displayPose.x,
            truckZ: displayPose.z,
            headingDegrees: displayPose.headingDegrees,
            zoom: zoom,
            speedKph: telemetry.speedKph,
            mode: viewMode,
            screenOffset: cameraOffset,
          );
    final truckPoint = transform.project(displayPose.x, displayPose.z);
    if (!hasRoute) {
      _paintTruck(
        canvas,
        truckPoint,
        NavigationHeading.markerRotationRadians(
          viewMode,
          displayPose.headingDegrees,
        ),
      );
      return;
    }

    Offset project(NativeRoutePoint point) =>
        transform.project(point.x, point.z);

    final activeProgress = progress?.routeId == route.routeId ? progress : null;
    final segmentIndex = math.min(
      math.max(activeProgress?.segmentIndex ?? 0, 0),
      route.points.length - 2,
    );
    final projectedPoint = activeProgress == null
        ? route.points[segmentIndex]
        : NativeRoutePoint(
            activeProgress.projectedX,
            activeProgress.projectedZ,
          );

    final travelledStart = viewMode == NavigationViewMode.overview
        ? 0
        : math.max(0, segmentIndex - 7);
    final travelled = <NativeRoutePoint>[
      ...route.points.sublist(travelledStart, segmentIndex + 1),
      projectedPoint,
    ];
    final remaining = <NativeRoutePoint>[
      projectedPoint,
      ...route.points.sublist(segmentIndex + 1),
    ];

    final distanceToProjection = math.sqrt(
      math.pow(displayPose.x - projectedPoint.x, 2) +
          math.pow(displayPose.z - projectedPoint.z, 2),
    );
    if (segmentIndex <= 2 &&
        !(activeProgress?.isOffRoute ?? false) &&
        distanceToProjection > 4 &&
        distanceToProjection < 180) {
      // Bridge the short depot-to-road gap only at the beginning of a route.
      remaining.insert(0, NativeRoutePoint(displayPose.x, displayPose.z));
    }

    final travelledPath = _smoothedPath(
      travelled.map(project).toList(growable: false),
    );
    final remainingPath = _smoothedPath(
      remaining.map(project).toList(growable: false),
    );
    final routeWidth = (6.4 + math.log(zoom.clamp(0.55, 3.4)) * 0.35).clamp(
      5.8,
      7.2,
    );

    _drawRouteLayer(
      canvas,
      travelledPath,
      casingWidth: routeWidth + 4,
      innerWidth: routeWidth - 1,
      innerColor: NavigationMapStyle.travelledRoute,
    );
    _drawRouteLayer(
      canvas,
      remainingPath,
      casingWidth: routeWidth + 6,
      innerWidth: routeWidth,
      innerColor: NavigationMapStyle.upcomingRoute,
    );
    canvas.drawPath(
      remainingPath,
      Paint()
        ..color = NavigationMapStyle.upcomingRouteHighlight.withValues(
          alpha: 0.7,
        )
        ..strokeWidth = 1.25
        ..strokeCap = StrokeCap.round
        ..strokeJoin = StrokeJoin.round
        ..style = PaintingStyle.stroke,
    );

    if (viewMode != NavigationViewMode.overview && zoom >= 0.62) {
      _paintDirectionChevrons(canvas, remainingPath, truckPoint);
    }

    final start = project(route.points.first);
    if (viewMode == NavigationViewMode.overview ||
        (Offset.zero & size).inflate(24).contains(start)) {
      _paintRouteFlag(canvas, start, const Color(0xFF35C96F));
    }
    final destination = project(route.points.last);
    if (viewMode == NavigationViewMode.overview ||
        (Offset.zero & size).inflate(24).contains(destination)) {
      _paintRouteFlag(
        canvas,
        destination,
        routeDestinationFlagColor(route.destination),
      );
    }
    _paintTruck(
      canvas,
      truckPoint,
      NavigationHeading.markerRotationRadians(
        viewMode,
        displayPose.headingDegrees,
      ),
    );
  }

  static void _drawRouteLayer(
    Canvas canvas,
    Path path, {
    required double casingWidth,
    required double innerWidth,
    required Color innerColor,
  }) {
    canvas.drawPath(
      path,
      Paint()
        ..color = NavigationMapStyle.routeCasing.withValues(alpha: 0.94)
        ..strokeWidth = casingWidth
        ..strokeCap = StrokeCap.round
        ..strokeJoin = StrokeJoin.round
        ..style = PaintingStyle.stroke,
    );
    canvas.drawPath(
      path,
      Paint()
        ..color = innerColor
        ..strokeWidth = innerWidth
        ..strokeCap = StrokeCap.round
        ..strokeJoin = StrokeJoin.round
        ..style = PaintingStyle.stroke,
    );
  }

  static Path _smoothedPath(List<Offset> points) {
    final path = Path();
    if (points.isEmpty) {
      return path;
    }
    path.moveTo(points.first.dx, points.first.dy);
    if (points.length == 1) {
      return path;
    }
    for (var index = 1; index < points.length - 1; index++) {
      final previous = points[index - 1];
      final current = points[index];
      final next = points[index + 1];
      final beforeVector = current - previous;
      final afterVector = next - current;
      final beforeLength = beforeVector.distance;
      final afterLength = afterVector.distance;
      if (beforeLength < 2 || afterLength < 2) {
        path.lineTo(current.dx, current.dy);
        continue;
      }
      final radius = math.min(10.0, math.min(beforeLength, afterLength) * 0.18);
      final before = current - beforeVector / beforeLength * radius;
      final after = current + afterVector / afterLength * radius;
      path
        ..lineTo(before.dx, before.dy)
        ..quadraticBezierTo(current.dx, current.dy, after.dx, after.dy);
    }
    path.lineTo(points.last.dx, points.last.dy);
    return path;
  }

  static void _paintDirectionChevrons(
    Canvas canvas,
    Path path,
    Offset truckPoint,
  ) {
    final paint = Paint()
      ..color = NavigationMapStyle.directionChevron
      ..style = PaintingStyle.fill;
    for (final metric in path.computeMetrics()) {
      if (metric.length < 135) {
        continue;
      }
      for (var distance = 62.0; distance < metric.length - 24; distance += 78) {
        final tangent = metric.getTangentForOffset(distance);
        if (tangent == null || (tangent.position - truckPoint).distance < 42) {
          continue;
        }
        canvas.save();
        canvas.translate(tangent.position.dx, tangent.position.dy);
        canvas.rotate(math.atan2(tangent.vector.dy, tangent.vector.dx));
        final chevron = Path()
          ..moveTo(8, 0)
          ..lineTo(-6, -7)
          ..lineTo(-3, 0)
          ..lineTo(-6, 7)
          ..close();
        canvas.drawShadow(chevron, const Color(0x99000000), 3, false);
        canvas.drawPath(chevron, paint);
        canvas.restore();
      }
    }
  }

  static void _paintRouteFlag(Canvas canvas, Offset point, Color color) {
    canvas.save();
    canvas.translate(point.dx, point.dy);
    canvas.drawCircle(
      Offset.zero,
      13,
      Paint()..color = NavigationMapStyle.routeCasing.withValues(alpha: 0.94),
    );
    final pole = Paint()
      ..color = Colors.white
      ..strokeWidth = 2
      ..strokeCap = StrokeCap.round;
    canvas.drawLine(const Offset(-3, 7), const Offset(-3, -8), pole);
    final flag = Path()
      ..moveTo(-2, -8)
      ..lineTo(7, -5)
      ..lineTo(-2, -1)
      ..close();
    canvas.drawPath(flag, Paint()..color = color);
    canvas.drawCircle(const Offset(-3, 8), 2.2, Paint()..color = color);
    canvas.restore();
  }

  static void _paintTruck(Canvas canvas, Offset point, double rotationRadians) {
    canvas.save();
    canvas.translate(point.dx, point.dy);
    canvas.rotate(rotationRadians);
    final marker = Path()
      ..moveTo(0, -20)
      ..lineTo(14, 14)
      ..lineTo(0, 9)
      ..lineTo(-14, 14)
      ..close();
    canvas.drawShadow(marker, Colors.black, 9, true);
    canvas.drawPath(
      marker,
      Paint()
        ..color = NavigationMapStyle.routeCasing
        ..style = PaintingStyle.fill,
    );
    final inset = Path()
      ..moveTo(0, -15)
      ..lineTo(9, 9)
      ..lineTo(0, 6)
      ..lineTo(-9, 9)
      ..close();
    canvas.drawPath(inset, Paint()..color = const Color(0xFFF5F7F8));
    canvas.drawLine(
      const Offset(0, -11),
      const Offset(0, 5),
      Paint()
        ..color = NavigationMapStyle.upcomingRouteHighlight
        ..strokeWidth = 3
        ..strokeCap = StrokeCap.round,
    );
    canvas.restore();
  }

  @override
  bool shouldRepaint(covariant NavigationPainter oldDelegate) =>
      telemetry != oldDelegate.telemetry ||
      route != oldDelegate.route ||
      progress != oldDelegate.progress ||
      zoom != oldDelegate.zoom ||
      viewMode != oldDelegate.viewMode ||
      pose != oldDelegate.pose ||
      cameraOffset != oldDelegate.cameraOffset;
}

class _ManeuverHud extends StatelessWidget {
  const _ManeuverHud({required this.progress});

  final NativeRouteProgress? progress;

  @override
  Widget build(BuildContext context) {
    final instruction =
        progress?.nextInstruction ?? 'Follow the highlighted route';
    final distance = progress?.distanceToNextManeuverMeters;
    final screenWidth = MediaQuery.sizeOf(context).width;
    final panelWidth = (screenWidth * 0.5).clamp(180.0, 230.0);
    return Positioned(
      left: 10,
      width: panelWidth,
      top: 10,
      child: Container(
        key: const Key('reference-maneuver-hud'),
        decoration: _referencePanelDecoration(radius: 8),
        clipBehavior: Clip.antiAlias,
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: <Widget>[
            SizedBox(
              height: 31,
              child: Center(
                child: Text(
                  TimeOfDay.now().format(context),
                  style: const TextStyle(
                    color: Color(0xFFBBC5CD),
                    fontSize: 14,
                    fontWeight: FontWeight.w700,
                    fontFeatures: <FontFeature>[FontFeature.tabularFigures()],
                  ),
                ),
              ),
            ),
            const Divider(height: 1, color: Color(0xFF3D4C57)),
            Padding(
              padding: const EdgeInsets.fromLTRB(10, 9, 10, 8),
              child: Row(
                children: <Widget>[
                  SizedBox(
                    width: 54,
                    child: Icon(
                      maneuverIconFor(progress?.nextManeuver),
                      color: NavigationMapStyle.upcomingRoute,
                      size: 48,
                    ),
                  ),
                  const SizedBox(width: 8),
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      mainAxisSize: MainAxisSize.min,
                      children: <Widget>[
                        Text(
                          distance == null ? '--' : _formatDistance(distance),
                          maxLines: 1,
                          style: const TextStyle(
                            color: NavigationMapStyle.upcomingRoute,
                            fontSize: 24,
                            height: 1,
                            fontWeight: FontWeight.w900,
                            fontFeatures: <FontFeature>[
                              FontFeature.tabularFigures(),
                            ],
                          ),
                        ),
                        const SizedBox(height: 7),
                        Text(
                          instruction,
                          maxLines: 2,
                          overflow: TextOverflow.ellipsis,
                          style: const TextStyle(
                            color: Color(0xFFF2F5F6),
                            fontSize: 14,
                            height: 1.05,
                            fontWeight: FontWeight.w800,
                          ),
                        ),
                      ],
                    ),
                  ),
                ],
              ),
            ),
            const Divider(
              height: 1,
              indent: 9,
              endIndent: 9,
              color: Color(0xFF263640),
            ),
            SizedBox(
              height: 31,
              child: Row(
                mainAxisAlignment: MainAxisAlignment.center,
                children: <Widget>[
                  Container(
                    width: 2,
                    height: 18,
                    color: const Color(0xFFDDE3E6),
                  ),
                  const SizedBox(width: 18),
                  Icon(
                    maneuverIconFor(progress?.nextManeuver),
                    color: const Color(0xFFF2F5F6),
                    size: 20,
                  ),
                  const SizedBox(width: 18),
                  Container(
                    width: 2,
                    height: 18,
                    color: const Color(0xFF5F6C74),
                  ),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _SpeedHud extends StatelessWidget {
  const _SpeedHud({required this.telemetry});

  final NativeTelemetryState telemetry;

  @override
  Widget build(BuildContext context) {
    return Container(
      key: const Key('reference-speed-hud'),
      decoration: _referencePanelDecoration(radius: 9),
      child: Padding(
        padding: const EdgeInsets.fromLTRB(11, 8, 10, 7),
        child: Row(
          mainAxisSize: MainAxisSize.min,
          children: <Widget>[
            Text(
              telemetry.speedKph.round().toString(),
              style: Theme.of(context).textTheme.titleLarge?.copyWith(
                color: Colors.white,
                fontWeight: FontWeight.w900,
                height: 1,
                fontFeatures: const <FontFeature>[FontFeature.tabularFigures()],
              ),
            ),
            const SizedBox(width: 4),
            const Text(
              'km/h',
              style: TextStyle(color: Color(0xFFABB6BE), fontSize: 11),
            ),
          ],
        ),
      ),
    );
  }
}

class _SpeedLimitBadge extends StatelessWidget {
  const _SpeedLimitBadge({required this.telemetry});

  final NativeTelemetryState telemetry;

  @override
  Widget build(BuildContext context) {
    final isAts = telemetry.game == TruckGame.americanTruckSimulator;
    return Container(
      key: const Key('reference-speed-limit-hud'),
      width: 55,
      height: 55,
      alignment: Alignment.center,
      decoration: BoxDecoration(
        color: Colors.white,
        shape: isAts ? BoxShape.rectangle : BoxShape.circle,
        borderRadius: isAts ? BorderRadius.circular(5) : null,
        border: Border.all(
          color: isAts ? Colors.black : const Color(0xFFD12626),
          width: isAts ? 3 : 4,
        ),
        boxShadow: const <BoxShadow>[
          BoxShadow(color: Color(0x99000000), blurRadius: 8),
        ],
      ),
      child: Text(
        telemetry.speedLimitKph.round().toString(),
        style: const TextStyle(
          color: Colors.black,
          fontSize: 20,
          fontWeight: FontWeight.w900,
        ),
      ),
    );
  }
}

class _TripHud extends StatelessWidget {
  const _TripHud({required this.progress, required this.route});

  final NativeRouteProgress? progress;
  final NativeRouteState route;

  @override
  Widget build(BuildContext context) {
    final remainingDistance =
        progress?.remainingDistanceMeters ?? route.totalDistanceMeters;
    final remainingTime =
        progress?.remainingTimeSeconds ?? route.estimatedTimeSeconds;
    final arrival = TimeOfDay.fromDateTime(
      DateTime.now().add(Duration(seconds: remainingTime.round())),
    ).format(context);
    return Container(
      key: const Key('reference-trip-hud'),
      width: 92,
      padding: const EdgeInsets.fromLTRB(9, 8, 9, 8),
      decoration: _referencePanelDecoration(radius: 9),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.end,
        mainAxisSize: MainAxisSize.min,
        children: <Widget>[
          Text(
            _formatDistance(remainingDistance),
            style: const TextStyle(
              color: Color(0xFFF3F5F6),
              fontSize: 14,
              fontWeight: FontWeight.w900,
              fontFeatures: <FontFeature>[FontFeature.tabularFigures()],
            ),
          ),
          const SizedBox(height: 5),
          Text(
            _formatTime(remainingTime),
            style: const TextStyle(
              color: NavigationMapStyle.upcomingRoute,
              fontSize: 14,
              fontWeight: FontWeight.w900,
              fontFeatures: <FontFeature>[FontFeature.tabularFigures()],
            ),
          ),
          const SizedBox(height: 7),
          SizedBox(
            width: double.infinity,
            child: FittedBox(
              fit: BoxFit.scaleDown,
              alignment: Alignment.centerRight,
              child: Row(
                mainAxisSize: MainAxisSize.min,
                children: <Widget>[
                  const Icon(
                    Icons.flag_rounded,
                    color: Color(0xFFE3E8EA),
                    size: 15,
                  ),
                  const SizedBox(width: 6),
                  Text(
                    arrival,
                    style: const TextStyle(
                      color: Color(0xFFF3F5F6),
                      fontSize: 12,
                      fontWeight: FontWeight.w800,
                      fontFeatures: <FontFeature>[FontFeature.tabularFigures()],
                    ),
                  ),
                ],
              ),
            ),
          ),
        ],
      ),
    );
  }
}

BoxDecoration _referencePanelDecoration({required double radius}) =>
    BoxDecoration(
      color: const Color(0xF208151F),
      borderRadius: BorderRadius.circular(radius),
      border: Border.all(color: const Color(0xFF40515D), width: 1.2),
      boxShadow: const <BoxShadow>[
        BoxShadow(
          color: Color(0x99000000),
          blurRadius: 12,
          offset: Offset(0, 4),
        ),
      ],
    );

class _NavigationStatus extends StatelessWidget {
  const _NavigationStatus({required this.telemetry, this.compact = false});

  final NativeTelemetryState telemetry;
  final bool compact;

  @override
  Widget build(BuildContext context) {
    final icon = !telemetry.connected
        ? Icons.sensors_off_rounded
        : telemetry.navigationStatus == 'NavigationDataMissing'
        ? Icons.map_outlined
        : Icons.route_rounded;
    final title = telemetry.navigationStatus == 'NavigationDataMissing'
        ? 'Navigation data needed'
        : telemetry.connected
        ? 'Preparing route'
        : 'Waiting for game telemetry';
    final card = Container(
      key: compact ? const Key('free-roam-status') : null,
      margin: compact ? EdgeInsets.zero : const EdgeInsets.all(28),
      padding: EdgeInsets.all(compact ? 13 : 22),
      decoration: BoxDecoration(
        color: const Color(0xED111820),
        borderRadius: BorderRadius.circular(compact ? 17 : 26),
        border: Border.all(color: TruckNavPalette.border),
        boxShadow: compact
            ? const <BoxShadow>[
                BoxShadow(color: Color(0x66000000), blurRadius: 14),
              ]
            : null,
      ),
      child: compact
          ? Row(
              children: <Widget>[
                Icon(icon, color: TruckNavPalette.accent, size: 25),
                const SizedBox(width: 10),
                Expanded(
                  child: Column(
                    mainAxisSize: MainAxisSize.min,
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: <Widget>[
                      Text(
                        title,
                        style: Theme.of(context).textTheme.titleSmall?.copyWith(
                          fontWeight: FontWeight.w900,
                        ),
                      ),
                      const SizedBox(height: 2),
                      Text(
                        telemetry.statusMessage,
                        maxLines: 2,
                        overflow: TextOverflow.ellipsis,
                        style: Theme.of(context).textTheme.bodySmall?.copyWith(
                          color: TruckNavPalette.textSecondary,
                          height: 1.2,
                        ),
                      ),
                    ],
                  ),
                ),
              ],
            )
          : Column(
              mainAxisSize: MainAxisSize.min,
              children: <Widget>[
                Icon(icon, color: TruckNavPalette.accent, size: 38),
                const SizedBox(height: 12),
                Text(
                  title,
                  textAlign: TextAlign.center,
                  style: Theme.of(
                    context,
                  ).textTheme.titleLarge?.copyWith(fontWeight: FontWeight.w900),
                ),
                const SizedBox(height: 7),
                Text(
                  telemetry.statusMessage,
                  textAlign: TextAlign.center,
                  style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                    color: TruckNavPalette.textSecondary,
                    height: 1.4,
                  ),
                ),
              ],
            ),
    );
    if (compact) {
      return card;
    }
    return Center(child: card);
  }
}

class _StatusPill extends StatelessWidget {
  const _StatusPill({
    required this.icon,
    required this.label,
    required this.color,
  });

  final IconData icon;
  final String label;
  final Color color;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 9),
      decoration: BoxDecoration(
        color: const Color(0xEB10171E),
        borderRadius: BorderRadius.circular(5),
        border: Border.all(color: TruckNavPalette.border),
      ),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: <Widget>[
          Icon(icon, size: 17, color: color),
          const SizedBox(width: 7),
          Flexible(
            child: Text(
              label,
              maxLines: 1,
              overflow: TextOverflow.ellipsis,
              style: const TextStyle(fontSize: 12, fontWeight: FontWeight.w700),
            ),
          ),
        ],
      ),
    );
  }
}

@visibleForTesting
IconData maneuverIconFor(String? maneuver) => switch (maneuver) {
  'SlightLeft' => Icons.turn_slight_left_rounded,
  'Left' => Icons.turn_left_rounded,
  'SharpLeft' => Icons.turn_sharp_left_rounded,
  'SlightRight' => Icons.turn_slight_right_rounded,
  'Right' => Icons.turn_right_rounded,
  'SharpRight' => Icons.turn_sharp_right_rounded,
  'UTurn' => Icons.u_turn_left_rounded,
  'Ferry' => Icons.directions_boat_rounded,
  'Arrive' => Icons.flag_rounded,
  _ => Icons.straight_rounded,
};

@visibleForTesting
Color routeDestinationFlagColor(String? destination) =>
    destination?.startsWith('Pickup:') ?? false
    ? NavigationMapStyle.upcomingRoute
    : const Color(0xFFF05252);

@visibleForTesting
String offRouteStatusLabel(double distanceMeters) =>
    'Off route - ${_formatDistance(distanceMeters)} from route';

String _formatDistance(double meters) {
  if (meters >= 1000) {
    return '${(meters / 1000).toStringAsFixed(meters >= 10000 ? 0 : 1)} km';
  }
  return '${math.max(0, meters).round()} m';
}

String _formatTime(double seconds) {
  final minutes = math.max(0, seconds / 60).round();
  if (minutes >= 60) {
    return '${minutes ~/ 60}h ${minutes % 60}m';
  }
  return '${math.max(1, minutes)} min';
}
