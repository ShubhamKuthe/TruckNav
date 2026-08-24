import 'dart:math' as math;

import 'package:flutter/material.dart';

enum NavigationViewMode { headingUp, worldUp, overview }

abstract final class NavigationHeading {
  static double normalizeDegrees(double degrees) {
    final normalized = degrees % 360;
    return normalized < 0 ? normalized + 360 : normalized;
  }

  static double toCanvasRadians(double navigationDegrees) =>
      normalizeDegrees(navigationDegrees) * math.pi / 180;

  /// TruckNav navigation bearings are clockwise with 0=north/-Z and 90=east/+X.
  static Offset worldForward(double navigationDegrees) {
    final radians = toCanvasRadians(navigationDegrees);
    return Offset(math.sin(radians), -math.cos(radians));
  }

  static double cameraRotationRadians(
    NavigationViewMode mode,
    double navigationDegrees,
  ) => mode == NavigationViewMode.headingUp
      ? -toCanvasRadians(navigationDegrees)
      : 0;

  static double markerRotationRadians(
    NavigationViewMode mode,
    double navigationDegrees,
  ) => mode == NavigationViewMode.headingUp
      ? 0
      : toCanvasRadians(navigationDegrees);

  static double shortestDelta(double fromDegrees, double toDegrees) =>
      ((toDegrees - fromDegrees + 540) % 360) - 180;
}

abstract final class NavigationZoomPolicy {
  static const double minimum = 0.25;
  static const double maximum = 1;
  static const double defaultFollowScale = 0.9;
  static const double maximumFollowScale = 0.9;

  static double clamp(double zoom) => zoom.clamp(minimum, maximum);

  static double fromGesture(double startZoom, double gestureScale) =>
      clamp(startZoom * gestureScale);

  static double adaptiveFactor({
    required double speedKph,
    double? maneuverDistanceMeters,
  }) {
    var factor = switch (speedKph) {
      < 12 => 1.16,
      < 35 => 1.07,
      > 85 => 0.82,
      > 60 => 0.9,
      _ => 1.0,
    };

    if (maneuverDistanceMeters case final distance?) {
      if (distance < 220) {
        factor *= 1.17;
      } else if (distance < 650) {
        factor *= 1.08;
      }
    }

    return factor.clamp(0.8, 1.24);
  }
}

abstract final class NavigationMapStyle {
  static const Color upcomingRoute = Color(0xFFFFB71B);
  static const Color upcomingRouteHighlight = Color(0xFFFFDC67);
  static const Color travelledRoute = Color(0xFF6D5420);
  static const Color routeCasing = Color(0xFF071018);
  static const Color directionChevron = Color(0xFF3DE66F);
  static const Color contextMajor = Color(0xFF69727C);
  static const Color contextMinor = Color(0xFF3C4651);
  static const Color contextCasing = Color(0xFF08131B);
  static const Color background = Color(0xFF07131C);
  static const Color lightBackground = Color(0xFFE8EFF2);
}

class NavigationCameraTransform {
  const NavigationCameraTransform._({
    required this.centerX,
    required this.centerZ,
    required this.scale,
    required this.screenOrigin,
    required this.rotationRadians,
    required this.truckAnchor,
  });

  static const double followTruckHeightFraction = 0.68;

  factory NavigationCameraTransform.follow({
    required Size size,
    required double truckX,
    required double truckZ,
    required double headingDegrees,
    required double zoom,
    required double speedKph,
    NavigationViewMode mode = NavigationViewMode.headingUp,
    Offset screenOffset = Offset.zero,
  }) {
    final safeZoom = NavigationZoomPolicy.clamp(zoom);
    final scale = (NavigationZoomPolicy.defaultFollowScale * safeZoom).clamp(
      0.025,
      NavigationZoomPolicy.maximumFollowScale,
    );
    final anchor = Offset(
      size.width / 2,
      size.height * followTruckHeightFraction,
    );

    if (mode != NavigationViewMode.headingUp) {
      return NavigationCameraTransform._(
        centerX: truckX,
        centerZ: truckZ,
        scale: scale,
        screenOrigin: anchor + screenOffset,
        rotationRadians: 0,
        truckAnchor: anchor + screenOffset,
      );
    }

    final lookAheadPixels = (size.height * 0.15 + speedKph.clamp(0, 100) * 0.22)
        .clamp(78.0, 148.0);
    final forward = NavigationHeading.worldForward(headingDegrees);
    final lookAheadMeters = lookAheadPixels / scale;
    return NavigationCameraTransform._(
      centerX: truckX + forward.dx * lookAheadMeters,
      centerZ: truckZ + forward.dy * lookAheadMeters,
      scale: scale,
      screenOrigin:
          Offset(anchor.dx, anchor.dy - lookAheadPixels) + screenOffset,
      rotationRadians: NavigationHeading.cameraRotationRadians(
        mode,
        headingDegrees,
      ),
      truckAnchor: anchor + screenOffset,
    );
  }

  factory NavigationCameraTransform.overview({
    required Size size,
    required List<Offset> worldPoints,
  }) {
    final minX = worldPoints.map((point) => point.dx).reduce(math.min);
    final maxX = worldPoints.map((point) => point.dx).reduce(math.max);
    final minZ = worldPoints.map((point) => point.dy).reduce(math.min);
    final maxZ = worldPoints.map((point) => point.dy).reduce(math.max);
    final availableWidth = math.max(100.0, size.width - 72);
    final availableHeight = math.max(100.0, size.height - 190);
    final scale = math
        .min(
          availableWidth / math.max(1, maxX - minX),
          availableHeight / math.max(1, maxZ - minZ),
        )
        .clamp(0.001, 0.42);
    final origin = Offset(size.width / 2, size.height * 0.55);
    return NavigationCameraTransform._(
      centerX: (minX + maxX) / 2,
      centerZ: (minZ + maxZ) / 2,
      scale: scale,
      screenOrigin: origin,
      rotationRadians: 0,
      truckAnchor: origin,
    );
  }

  final double centerX;
  final double centerZ;
  final double scale;
  final Offset screenOrigin;
  final double rotationRadians;
  final Offset truckAnchor;

  Offset project(double x, double z) {
    final dx = x - centerX;
    final dz = z - centerZ;
    final cosine = math.cos(rotationRadians);
    final sine = math.sin(rotationRadians);
    final rotatedX = dx * cosine - dz * sine;
    final rotatedY = dx * sine + dz * cosine;
    return screenOrigin + Offset(rotatedX * scale, rotatedY * scale);
  }

  Offset unproject(Offset screenPoint) {
    final rotated = (screenPoint - screenOrigin) / scale;
    final cosine = math.cos(rotationRadians);
    final sine = math.sin(rotationRadians);
    final dx = rotated.dx * cosine + rotated.dy * sine;
    final dz = -rotated.dx * sine + rotated.dy * cosine;
    return Offset(centerX + dx, centerZ + dz);
  }
}
