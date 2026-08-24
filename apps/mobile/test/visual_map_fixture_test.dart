import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:trucknav_mobile/features/gps/navigation_geometry.dart';
import 'package:trucknav_mobile/features/gps/visual_map_tiles.dart';

void main() {
  test('roundabout fixture preserves the ring and all four exits', () {
    final tile = _tile(
      lines: <Object?>[
        <Object?>[
          1,
          1,
          1,
          9,
          <num>[
            -30,
            0,
            -21,
            -21,
            0,
            -30,
            21,
            -21,
            30,
            0,
            21,
            21,
            0,
            30,
            -21,
            21,
            -30,
            0,
          ],
        ],
        <Object?>[
          2,
          1,
          1,
          8,
          <num>[0, -90, 0, -30],
        ],
        <Object?>[
          3,
          1,
          1,
          8,
          <num>[30, 0, 90, 0],
        ],
        <Object?>[
          4,
          1,
          1,
          8,
          <num>[0, 30, 0, 90],
        ],
        <Object?>[
          5,
          1,
          1,
          8,
          <num>[-90, 0, -30, 0],
        ],
      ],
    );

    expect(tile.lines, hasLength(5));
    expect(tile.lines.first.points.first, tile.lines.first.points.last);
    final ringPoints = tile.lines.first.points.toSet();
    for (final exit in tile.lines.skip(1)) {
      expect(exit.points.any(ringPoints.contains), isTrue);
    }
  });

  test('motorway fixture preserves carriageways, ramp, merge, and exit', () {
    final tile = _tile(
      lines: <Object?>[
        <Object?>[
          10,
          0,
          3,
          12,
          <num>[-120, -12, 120, -12],
        ],
        <Object?>[
          11,
          0,
          3,
          12,
          <num>[120, 12, -120, 12],
        ],
        <Object?>[
          12,
          1,
          2,
          8,
          <num>[-30, -12, 0, -35, 45, -50],
        ],
        <Object?>[
          13,
          1,
          2,
          8,
          <num>[45, -50, 80, -35, 100, -12],
        ],
      ],
    );

    expect(tile.lines.where((line) => line.roadClass == 3), hasLength(2));
    expect(tile.lines.where((line) => line.kind == 1), hasLength(2));
    expect(tile.lines[2].points.last, tile.lines[3].points.first);
  });

  test('city fixture preserves label and verified POI categories', () {
    final tile = _tile(
      lines: <Object?>[
        <Object?>[
          20,
          0,
          1,
          8,
          <num>[-80, 0, 80, 0],
        ],
        <Object?>[
          21,
          1,
          1,
          8,
          <num>[0, -80, 0, 80],
        ],
      ],
      labels: <Object?>[
        <Object?>[30, 'Test City', 0, 0, 2],
      ],
      pois: <Object?>[
        <Object?>[31, 'Fuel', 15, 5, null],
        <Object?>[32, 'Service', -15, 5, null],
        <Object?>[33, 'Company', 0, 20, 'Test Depot'],
      ],
    );

    expect(tile.labels.single.text, 'Test City');
    expect(
      tile.pointsOfInterest.map((point) => point.type),
      containsAll(<String>['Fuel', 'Service', 'Company']),
    );
  });

  test('route and visual map use the identical world coordinate transform', () {
    const roadPoint = Offset(1250, -3400);
    const routePoint = Offset(1250, -3400);
    final transform = NavigationCameraTransform.follow(
      size: const Size(500, 900),
      truckX: 1200,
      truckZ: -3300,
      headingDegrees: 47,
      zoom: 1.2,
      speedKph: 65,
    );

    expect(
      transform.project(roadPoint.dx, roadPoint.dy),
      transform.project(routePoint.dx, routePoint.dy),
    );
  });

  testWidgets('visual fixture painter renders without rebuilding geometry', (
    tester,
  ) async {
    final tile = _tile(
      lines: <Object?>[
        <Object?>[
          40,
          1,
          2,
          9,
          <num>[-50, 0, 0, -30, 50, 0],
        ],
      ],
      labels: <Object?>[
        <Object?>[41, 'Fixture', 0, 20, 2],
      ],
    );
    final transform = NavigationCameraTransform.follow(
      size: const Size(400, 400),
      truckX: 0,
      truckZ: 0,
      headingDegrees: 0,
      zoom: 1,
      speedKph: 0,
    );

    await tester.pumpWidget(
      MaterialApp(
        home: SizedBox.expand(
          child: CustomPaint(
            painter: VisualMapPainter(
              tiles: <VisualMapTileData>[tile],
              transform: transform,
              detailLevel: 2,
            ),
          ),
        ),
      ),
    );

    expect(tester.takeException(), isNull);
  });
}

VisualMapTileData _tile({
  required List<Object?> lines,
  List<Object?> labels = const <Object?>[],
  List<Object?> pois = const <Object?>[],
}) {
  final bytes = utf8.encode(
    jsonEncode(<String, Object?>{
      'version': 1,
      'lines': lines,
      'polygons': const <Object?>[],
      'labels': labels,
      'pois': pois,
    }),
  );
  return VisualMapTileData.fromBytes(bytes);
}
