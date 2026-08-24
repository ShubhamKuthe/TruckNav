import 'dart:math' as math;
import 'dart:ui' as ui;

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:trucknav_mobile/features/gps/native_navigation_map.dart';
import 'package:trucknav_mobile/features/session/session_state.dart';

void main() {
  test('NavigationPose interpolates position and shortest wrapped heading', () {
    const from = NavigationPose(x: 0, z: 0, headingDegrees: 359);
    const to = NavigationPose(x: 100, z: 50, headingDegrees: 1);

    final middle = NavigationPose.interpolate(from, to, 0.5);

    expect(middle.x, 50);
    expect(middle.z, 25);
    expect(middle.headingDegrees, closeTo(0, 0.001));
  });

  test('NavigationPose snaps across a teleport', () {
    const from = NavigationPose(x: 0, z: 0, headingDegrees: 10);
    const to = NavigationPose(x: 2500, z: 0, headingDegrees: 180);

    final middle = NavigationPose.interpolate(from, to, 0.2);

    expect(middle.x, to.x);
    expect(middle.z, to.z);
    expect(middle.headingDegrees, to.headingDegrees);
  });

  test('canonical navigation heading uses north -Z and clockwise bearings', () {
    expect(NavigationHeading.worldForward(0), const Offset(0, -1));
    expect(NavigationHeading.worldForward(90).dx, closeTo(1, 0.000001));
    expect(NavigationHeading.worldForward(90).dy, closeTo(0, 0.000001));
    expect(NavigationHeading.worldForward(180).dy, closeTo(1, 0.000001));
    expect(NavigationHeading.worldForward(270).dx, closeTo(-1, 0.000001));
    expect(NavigationHeading.shortestDelta(359, 0), 1);
  });

  test(
    'heading-up transform keeps truck at 68 percent and route ahead above',
    () {
      const size = Size(400, 800);
      final transform = NavigationCameraTransform.follow(
        size: size,
        truckX: 1000,
        truckZ: 2000,
        headingDegrees: 90,
        zoom: 1,
        speedKph: 60,
      );
      final forward = NavigationHeading.worldForward(90);
      final truck = transform.project(1000, 2000);
      final ahead = transform.project(
        1000 + forward.dx * 500,
        2000 + forward.dy * 500,
      );
      final behind = transform.project(
        1000 - forward.dx * 500,
        2000 - forward.dy * 500,
      );

      expect(truck.dx, closeTo(size.width / 2, 0.001));
      expect(truck.dy, closeTo(size.height * 0.68, 0.001));
      expect(
        transform.scale,
        closeTo(NavigationZoomPolicy.defaultFollowScale, 0.000001),
      );
      expect(ahead.dy, lessThan(truck.dy));
      expect(behind.dy, greaterThan(truck.dy));
    },
  );

  test('world-up camera does not rotate map and rotates marker clockwise', () {
    final transform = NavigationCameraTransform.follow(
      size: const Size(400, 800),
      truckX: 0,
      truckZ: 0,
      headingDegrees: 90,
      zoom: 1,
      speedKph: 20,
      mode: NavigationViewMode.worldUp,
    );

    expect(transform.rotationRadians, 0);
    expect(
      NavigationHeading.markerRotationRadians(NavigationViewMode.worldUp, 90),
      closeTo(math.pi / 2, 0.000001),
    );
    expect(
      NavigationHeading.markerRotationRadians(NavigationViewMode.headingUp, 90),
      0,
    );
  });

  test(
    'stationary heading noise is damped but meaningful turns are retained',
    () {
      const current = NavigationPose(x: 0, z: 0, headingDegrees: 10);
      const noisy = NavigationPose(x: 0, z: 0, headingDegrees: 13);
      const turn = NavigationPose(x: 0, z: 0, headingDegrees: 40);

      expect(
        NavigationPose.stabilizeHeading(current, noisy, 0).headingDegrees,
        10,
      );
      expect(
        NavigationPose.stabilizeHeading(current, turn, 0).headingDegrees,
        40,
      );
    },
  );

  test('pinch zoom follows standard direction and clamps safely', () {
    expect(NavigationZoomPolicy.fromGesture(1, 1.5), 1);
    expect(NavigationZoomPolicy.fromGesture(1, 0.7), 0.7);
    expect(
      NavigationZoomPolicy.fromGesture(3, 3),
      NavigationZoomPolicy.maximum,
    );
    expect(
      NavigationZoomPolicy.fromGesture(0.6, 0.1),
      NavigationZoomPolicy.minimum,
    );
  });

  test('default road-level camera uses the requested 90 percent scale', () {
    final transform = NavigationCameraTransform.follow(
      size: const Size(400, 800),
      truckX: 0,
      truckZ: 0,
      headingDegrees: 0,
      zoom: NavigationZoomPolicy.maximum,
      speedKph: 50,
    );

    expect(NavigationZoomPolicy.defaultFollowScale, 0.9);
    expect(transform.scale, 0.9);
  });

  test('off-route warning includes a driver-friendly live distance', () {
    expect(offRouteStatusLabel(126), 'Off route - 126 m from route');
    expect(offRouteStatusLabel(1450), 'Off route - 1.4 km from route');
  });

  test('free roam paints the truck triangle without an active route', () async {
    const telemetry = NativeTelemetryState(
      game: TruckGame.euroTruckSimulator2,
      connected: true,
      paused: false,
      x: 100,
      z: 200,
      elevation: 0,
      headingDegrees: 0,
      speedKph: 0,
      speedLimitKph: 80,
      onJob: false,
      destinationCity: null,
      destinationCompany: null,
      gameNavigationDistanceMeters: 0,
      gameNavigationTimeSeconds: 0,
      navigationStatus: 'FreeRoam',
      statusMessage: 'Free roam',
      timestampUtc: null,
    );
    const size = Size(200, 300);
    final recorder = ui.PictureRecorder();
    final painter = NavigationPainter(
      telemetry: telemetry,
      route: const NativeRouteState.empty(),
      progress: null,
      zoom: 1,
      viewMode: NavigationViewMode.headingUp,
      pose: const NavigationPose(x: 100, z: 200, headingDegrees: 0),
    );
    painter.paint(Canvas(recorder), size);
    final image = await recorder.endRecording().toImage(
      size.width.round(),
      size.height.round(),
    );
    final bytes = await image.toByteData(format: ui.ImageByteFormat.rawRgba);
    final anchorX = (size.width / 2).round();
    final anchorY = (size.height * 0.68).round();
    final alphaIndex = (anchorY * size.width.round() + anchorX) * 4 + 3;

    expect(bytes, isNotNull);
    expect(bytes!.getUint8(alphaIndex), greaterThan(0));
    image.dispose();
  });

  testWidgets('free roam status is compact and positioned at top right', (
    tester,
  ) async {
    const telemetry = NativeTelemetryState(
      game: TruckGame.euroTruckSimulator2,
      connected: true,
      paused: false,
      x: 100,
      z: 200,
      elevation: 0,
      headingDegrees: 0,
      speedKph: 0,
      speedLimitKph: 80,
      onJob: false,
      destinationCity: null,
      destinationCompany: null,
      gameNavigationDistanceMeters: 0,
      gameNavigationTimeSeconds: 0,
      navigationStatus: 'FreeRoam',
      statusMessage: 'Accept a delivery job to create a route.',
      timestampUtc: null,
    );
    await tester.pumpWidget(
      const MaterialApp(
        home: NativeNavigationMap(
          telemetry: telemetry,
          route: NativeRouteState.empty(),
          progress: null,
          zoom: 1,
          viewMode: NavigationViewMode.headingUp,
        ),
      ),
    );

    final status = find.byKey(const Key('free-roam-status'));
    expect(status, findsOneWidget);
    expect(tester.getTopLeft(status).dx, greaterThan(400));
    expect(tester.getTopLeft(status).dy, closeTo(64, 0.1));
  });

  testWidgets('paused game is clearly identified on the navigation map', (
    tester,
  ) async {
    const telemetry = NativeTelemetryState(
      game: TruckGame.euroTruckSimulator2,
      connected: true,
      paused: true,
      x: 100,
      z: 200,
      elevation: 0,
      headingDegrees: 0,
      speedKph: 0,
      speedLimitKph: 80,
      onJob: false,
      destinationCity: null,
      destinationCompany: null,
      gameNavigationDistanceMeters: 0,
      gameNavigationTimeSeconds: 0,
      navigationStatus: 'FreeRoam',
      statusMessage: 'Free roam',
      timestampUtc: null,
    );
    await tester.pumpWidget(
      const MaterialApp(
        home: NativeNavigationMap(
          telemetry: telemetry,
          route: NativeRouteState.empty(),
          progress: null,
          zoom: 1,
          viewMode: NavigationViewMode.headingUp,
        ),
      ),
    );

    expect(find.text('GAME PAUSED'), findsOneWidget);
  });

  testWidgets('reference navigation HUD stays aligned to the screen edges', (
    tester,
  ) async {
    tester.view.physicalSize = const Size(430, 850);
    tester.view.devicePixelRatio = 1;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);
    const telemetry = NativeTelemetryState(
      game: TruckGame.euroTruckSimulator2,
      connected: true,
      paused: false,
      x: 0,
      z: 0,
      elevation: 0,
      headingDegrees: 0,
      speedKph: 0,
      speedLimitKph: 90,
      onJob: true,
      destinationCity: 'Riga',
      destinationCompany: 'Tradeaux',
      gameNavigationDistanceMeters: 12000,
      gameNavigationTimeSeconds: 600,
      navigationStatus: 'Navigating',
      statusMessage: 'Route active',
      timestampUtc: null,
    );
    const route = NativeRouteState(
      revision: 1,
      available: true,
      routeId: 'route-1',
      game: TruckGame.euroTruckSimulator2,
      destination: 'Riga',
      points: <NativeRoutePoint>[
        NativeRoutePoint(0, 0),
        NativeRoutePoint(0, -500),
        NativeRoutePoint(400, -900),
      ],
      steps: <NativeRouteStep>[],
      contextRoads: <NativeContextRoad>[],
      mapPoints: <NativeMapPoint>[],
      mapLabels: <NativeMapLabel>[],
      totalDistanceMeters: 12000,
      estimatedTimeSeconds: 600,
      calculatedUtc: null,
      isReroute: false,
    );
    const progress = NativeRouteProgress(
      routeId: 'route-1',
      segmentIndex: 0,
      projectedX: 0,
      projectedZ: 0,
      distanceFromRouteMeters: 0,
      remainingDistanceMeters: 12000,
      remainingTimeSeconds: 600,
      nextManeuver: 'Right',
      nextInstruction: 'Turn right',
      distanceToNextManeuverMeters: 450,
      isOffRoute: false,
      teleportDetected: false,
      timestampUtc: null,
    );

    await tester.pumpWidget(
      const MaterialApp(
        home: NativeNavigationMap(
          telemetry: telemetry,
          route: route,
          progress: progress,
          zoom: 1,
          viewMode: NavigationViewMode.headingUp,
        ),
      ),
    );

    final maneuver = find.byKey(const Key('reference-maneuver-hud'));
    final trip = find.byKey(const Key('reference-trip-hud'));
    final speedLimit = find.byKey(const Key('reference-speed-limit-hud'));
    final speed = find.byKey(const Key('reference-speed-hud'));
    expect(tester.getTopLeft(maneuver), const Offset(10, 10));
    expect(tester.getTopRight(trip).dx, closeTo(420, 0.1));
    expect(tester.getTopLeft(trip).dy, closeTo(10, 0.1));
    expect(tester.getTopLeft(speedLimit).dx, closeTo(10, 0.1));
    expect(
      tester.getTopLeft(speedLimit).dy,
      lessThan(tester.getTopLeft(speed).dy),
    );
    expect(find.text('Turn right'), findsOneWidget);
    expect(find.text('12 km'), findsOneWidget);
    expect(find.text('10 min'), findsOneWidget);
    expect(tester.takeException(), isNull);
  });

  test('route painter uses amber route and green direction roles', () {
    expect(NavigationMapStyle.upcomingRoute, const Color(0xFFFFB71B));
    expect(NavigationMapStyle.travelledRoute, const Color(0xFF6D5420));
    expect(NavigationMapStyle.directionChevron, const Color(0xFF3DE66F));
    expect(
      NavigationMapStyle.upcomingRoute,
      isNot(TruckNavPaletteForTest.legacyRed),
    );
  });

  test('pickup endpoint uses amber while delivery endpoint stays red', () {
    expect(
      routeDestinationFlagColor('Pickup: Tradeaux, Riga'),
      NavigationMapStyle.upcomingRoute,
    );
    expect(
      routeDestinationFlagColor('Tradeaux, Riga'),
      const Color(0xFFF05252),
    );
  });

  test('maneuver icons retain specific left and right severity', () {
    expect(maneuverIconFor('SlightLeft'), Icons.turn_slight_left_rounded);
    expect(maneuverIconFor('Left'), Icons.turn_left_rounded);
    expect(maneuverIconFor('SharpLeft'), Icons.turn_sharp_left_rounded);
    expect(maneuverIconFor('SlightRight'), Icons.turn_slight_right_rounded);
    expect(maneuverIconFor('Right'), Icons.turn_right_rounded);
    expect(maneuverIconFor('SharpRight'), Icons.turn_sharp_right_rounded);
  });
}

abstract final class TruckNavPaletteForTest {
  static const Color legacyRed = Color(0xFFE63B36);
}
