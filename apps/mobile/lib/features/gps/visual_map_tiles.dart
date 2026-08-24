import 'dart:async';
import 'dart:collection';
import 'dart:convert';
import 'dart:io';
import 'dart:math' as math;

import 'package:flutter/material.dart';

import '../session/session_state.dart';
import 'navigation_geometry.dart';

enum VisualMapLoadState { loading, ready, unavailable }

class VisualMapManifest {
  const VisualMapManifest({
    required this.gameVersion,
    required this.generatorVersion,
    required this.generatedUtc,
    required this.levels,
  });

  factory VisualMapManifest.fromJson(Map<String, Object?> json) {
    final levels = (json['levels'] as List<dynamic>? ?? const <dynamic>[])
        .whereType<Map>()
        .map((value) {
          final item = Map<String, Object?>.from(value);
          return VisualMapLevel(
            level: (item['level'] as num?)?.toInt() ?? 0,
            tileWorldSize: (item['tileWorldSize'] as num?)?.toDouble() ?? 1,
            detail: item['detail'] as String? ?? '',
          );
        })
        .toList(growable: false);
    return VisualMapManifest(
      gameVersion: json['gameVersion'] as String? ?? '',
      generatorVersion: json['mapGeneratorVersion'] as String? ?? '',
      generatedUtc:
          DateTime.tryParse(json['generatedUtc'] as String? ?? '') ??
          DateTime.fromMillisecondsSinceEpoch(0, isUtc: true),
      levels: levels,
    );
  }

  final String gameVersion;
  final String generatorVersion;
  final DateTime generatedUtc;
  final List<VisualMapLevel> levels;
}

class VisualMapLevel {
  const VisualMapLevel({
    required this.level,
    required this.tileWorldSize,
    required this.detail,
  });

  final int level;
  final double tileWorldSize;
  final String detail;
}

class VisualMapTileKey {
  const VisualMapTileKey(this.level, this.x, this.y);

  final int level;
  final int x;
  final int y;

  @override
  bool operator ==(Object other) =>
      other is VisualMapTileKey &&
      other.level == level &&
      other.x == x &&
      other.y == y;

  @override
  int get hashCode => Object.hash(level, x, y);

  @override
  String toString() => '$level/$x/$y';
}

class VisualMapLine {
  VisualMapLine({
    required this.id,
    required this.kind,
    required this.roadClass,
    required this.widthMeters,
    required this.points,
  }) : worldPath = _openPath(points);

  final int id;
  final int kind;
  final int roadClass;
  final double widthMeters;
  final List<Offset> points;
  final Path worldPath;
}

class VisualMapPolygon {
  VisualMapPolygon({
    required this.id,
    required this.color,
    required this.drawOver,
    required this.points,
  }) : worldPath = _closedPath(points);

  final int id;
  final int color;
  final bool drawOver;
  final List<Offset> points;
  final Path worldPath;
}

Path _openPath(List<Offset> points) {
  final path = Path();
  if (points.isEmpty) {
    return path;
  }
  path.moveTo(points.first.dx, points.first.dy);
  for (final point in points.skip(1)) {
    path.lineTo(point.dx, point.dy);
  }
  return path;
}

Path _closedPath(List<Offset> points) => _openPath(points)..close();

class VisualMapLabelData {
  const VisualMapLabelData({
    required this.id,
    required this.text,
    required this.position,
    required this.priority,
  });

  final int id;
  final String text;
  final Offset position;
  final int priority;
}

class VisualMapPoiData {
  const VisualMapPoiData({
    required this.id,
    required this.type,
    required this.position,
    required this.label,
  });

  final int id;
  final String type;
  final Offset position;
  final String? label;
}

class VisualMapTileData {
  const VisualMapTileData({
    required this.lines,
    required this.polygons,
    required this.labels,
    required this.pointsOfInterest,
  });

  factory VisualMapTileData.fromBytes(List<int> bytes) {
    final decoded = jsonDecode(utf8.decode(bytes));
    if (decoded is! Map<String, Object?>) {
      throw const FormatException('Visual map tile must be a JSON object.');
    }

    return VisualMapTileData(
      lines: (decoded['lines'] as List<dynamic>? ?? const <dynamic>[])
          .whereType<List>()
          .where((value) => value.length >= 5)
          .map(
            (value) => VisualMapLine(
              id: (value[0] as num).toInt(),
              kind: (value[1] as num).toInt(),
              roadClass: (value[2] as num).toInt(),
              widthMeters: (value[3] as num).toDouble(),
              points: _parsePoints(value[4]),
            ),
          )
          .toList(growable: false),
      polygons: (decoded['polygons'] as List<dynamic>? ?? const <dynamic>[])
          .whereType<List>()
          .where((value) => value.length >= 4)
          .map(
            (value) => VisualMapPolygon(
              id: (value[0] as num).toInt(),
              color: (value[1] as num).toInt(),
              drawOver: value[2] as bool? ?? false,
              points: _parsePoints(value[3]),
            ),
          )
          .toList(growable: false),
      labels: (decoded['labels'] as List<dynamic>? ?? const <dynamic>[])
          .whereType<List>()
          .where((value) => value.length >= 5)
          .map(
            (value) => VisualMapLabelData(
              id: (value[0] as num).toInt(),
              text: value[1] as String? ?? '',
              position: Offset(
                (value[2] as num).toDouble(),
                (value[3] as num).toDouble(),
              ),
              priority: (value[4] as num).toInt(),
            ),
          )
          .toList(growable: false),
      pointsOfInterest: (decoded['pois'] as List<dynamic>? ?? const <dynamic>[])
          .whereType<List>()
          .where((value) => value.length >= 5)
          .map(
            (value) => VisualMapPoiData(
              id: (value[0] as num).toInt(),
              type: value[1] as String? ?? 'Other',
              position: Offset(
                (value[2] as num).toDouble(),
                (value[3] as num).toDouble(),
              ),
              label: value[4] as String?,
            ),
          )
          .toList(growable: false),
    );
  }

  final List<VisualMapLine> lines;
  final List<VisualMapPolygon> polygons;
  final List<VisualMapLabelData> labels;
  final List<VisualMapPoiData> pointsOfInterest;

  static List<Offset> _parsePoints(Object? value) {
    final coordinates = value as List<dynamic>? ?? const <dynamic>[];
    final points = <Offset>[];
    for (var index = 0; index + 1 < coordinates.length; index += 2) {
      final x = coordinates[index];
      final z = coordinates[index + 1];
      if (x is num && z is num) {
        points.add(Offset(x.toDouble(), z.toDouble()));
      }
    }
    return points;
  }
}

class VisualMapTileRepository {
  VisualMapTileRepository(this.connection)
    : _client = HttpClient()..connectionTimeout = const Duration(seconds: 5);

  static const int _memoryLimit = 72;

  final VisualMapConnection connection;
  final HttpClient _client;
  final LinkedHashMap<String, VisualMapTileData> _memoryCache =
      LinkedHashMap<String, VisualMapTileData>();

  Future<VisualMapManifest> fetchManifest(String game) async {
    final response = await _get('/map/$game/manifest');
    if (response.statusCode != HttpStatus.ok) {
      await response.drain<void>();
      throw HttpException(
        'Map manifest is unavailable (${response.statusCode}).',
      );
    }
    final bytes = await _readBytes(response);
    final decoded = jsonDecode(utf8.decode(bytes));
    if (decoded is! Map<String, Object?>) {
      throw const FormatException('Visual map manifest must be a JSON object.');
    }
    return VisualMapManifest.fromJson(decoded);
  }

  Future<VisualMapTileData> fetchTile({
    required String game,
    required VisualMapManifest manifest,
    required VisualMapTileKey key,
  }) async {
    final cacheKey =
        '$game/${manifest.generatedUtc.microsecondsSinceEpoch}/$key';
    final memoryValue = _memoryCache.remove(cacheKey);
    if (memoryValue != null) {
      _memoryCache[cacheKey] = memoryValue;
      return memoryValue;
    }

    final file = File(
      '${Directory.systemTemp.path}/trucknav-map-tiles/'
      '$game/${manifest.generatedUtc.microsecondsSinceEpoch}/'
      '${key.level}/${key.x}/${key.y}.json',
    );
    List<int>? bytes;
    if (await file.exists()) {
      bytes = await file.readAsBytes();
    } else {
      final response = await _get('/map/$game/${key.level}/${key.x}/${key.y}');
      if (response.statusCode != HttpStatus.ok) {
        await response.drain<void>();
        throw HttpException(
          'Visual map tile $key is unavailable (${response.statusCode}).',
        );
      }
      bytes = await _readBytes(response);
      await file.parent.create(recursive: true);
      await file.writeAsBytes(bytes, flush: false);
    }

    final tile = VisualMapTileData.fromBytes(bytes);
    _memoryCache[cacheKey] = tile;
    while (_memoryCache.length > _memoryLimit) {
      _memoryCache.remove(_memoryCache.keys.first);
    }
    return tile;
  }

  Future<HttpClientResponse> _get(String path) async {
    if (connection.accessToken.isEmpty || connection.host.isEmpty) {
      throw const HttpException('The paired map credential is unavailable.');
    }
    final request = await _client.getUrl(connection.uri(path));
    request.headers.set('X-TruckNav-Map-Token', connection.accessToken);
    request.headers.set(HttpHeaders.acceptHeader, 'application/json');
    return request.close();
  }

  static Future<List<int>> _readBytes(HttpClientResponse response) =>
      response.fold<List<int>>(<int>[], (buffer, data) => buffer..addAll(data));

  void dispose() => _client.close(force: true);
}

class VisualMapTileLayer extends StatefulWidget {
  const VisualMapTileLayer({
    required this.connection,
    required this.telemetry,
    required this.route,
    required this.zoom,
    required this.viewMode,
    required this.displayX,
    required this.displayZ,
    required this.displayHeadingDegrees,
    required this.cameraOffset,
    required this.darkMode,
    this.visiblePoiTypes,
    super.key,
  });

  final VisualMapConnection? connection;
  final NativeTelemetryState telemetry;
  final NativeRouteState route;
  final double zoom;
  final NavigationViewMode viewMode;
  final double displayX;
  final double displayZ;
  final double displayHeadingDegrees;
  final Offset cameraOffset;
  final bool darkMode;
  final Set<String>? visiblePoiTypes;

  @override
  State<VisualMapTileLayer> createState() => _VisualMapTileLayerState();
}

class _VisualMapTileLayerState extends State<VisualMapTileLayer> {
  VisualMapTileRepository? _repository;
  VisualMapManifest? _manifest;
  VisualMapLoadState _loadState = VisualMapLoadState.loading;
  Map<VisualMapTileKey, VisualMapTileData> _tiles =
      <VisualMapTileKey, VisualMapTileData>{};
  List<VisualMapTileData> _paintTiles = const <VisualMapTileData>[];
  Timer? _loadTimer;
  String _desiredSignature = '';
  int _generation = 0;

  String get _gameCode => switch (widget.telemetry.game) {
    TruckGame.euroTruckSimulator2 => 'ets2',
    TruckGame.americanTruckSimulator => 'ats',
    TruckGame.none => switch (widget.route.game) {
      TruckGame.euroTruckSimulator2 => 'ets2',
      TruckGame.americanTruckSimulator => 'ats',
      TruckGame.none => '',
    },
  };

  @override
  void initState() {
    super.initState();
    unawaited(_resetRepository());
  }

  @override
  void didUpdateWidget(covariant VisualMapTileLayer oldWidget) {
    super.didUpdateWidget(oldWidget);
    if (oldWidget.connection != widget.connection ||
        oldWidget.telemetry.game != widget.telemetry.game ||
        oldWidget.route.game != widget.route.game) {
      unawaited(_resetRepository());
    }
  }

  Future<void> _resetRepository() async {
    final generation = ++_generation;
    _loadTimer?.cancel();
    _repository?.dispose();
    _repository = null;
    _manifest = null;
    _tiles = <VisualMapTileKey, VisualMapTileData>{};
    _paintTiles = const <VisualMapTileData>[];
    _desiredSignature = '';
    if (mounted) {
      setState(() => _loadState = VisualMapLoadState.loading);
    }

    final connection = widget.connection;
    final game = _gameCode;
    if (connection == null || game.isEmpty) {
      if (mounted && generation == _generation) {
        setState(() => _loadState = VisualMapLoadState.unavailable);
      }
      return;
    }

    final repository = VisualMapTileRepository(connection);
    _repository = repository;
    try {
      final manifest = await repository.fetchManifest(game);
      if (!mounted || generation != _generation) {
        return;
      }
      setState(() {
        _manifest = manifest;
        _loadState = VisualMapLoadState.ready;
      });
    } catch (_) {
      if (mounted && generation == _generation) {
        setState(() => _loadState = VisualMapLoadState.unavailable);
      }
    }
  }

  @override
  void dispose() {
    _generation++;
    _loadTimer?.cancel();
    _repository?.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return ColoredBox(
      color: widget.darkMode
          ? NavigationMapStyle.background
          : NavigationMapStyle.lightBackground,
      child: LayoutBuilder(
        builder: (context, constraints) {
          final size = Size(constraints.maxWidth, constraints.maxHeight);
          final transform = _createTransform(size);
          final manifest = _manifest;
          if (manifest != null) {
            _scheduleTiles(size, transform, manifest);
          }
          return Stack(
            fit: StackFit.expand,
            children: <Widget>[
              CustomPaint(
                painter: VisualMapPainter(
                  tiles: _paintTiles,
                  transform: transform,
                  detailLevel:
                      _selectLevel(manifest, transform.scale)?.level ?? 0,
                  visiblePoiTypes: widget.visiblePoiTypes,
                  darkMode: widget.darkMode,
                ),
              ),
              if (_loadState != VisualMapLoadState.ready)
                Center(
                  child: DecoratedBox(
                    decoration: BoxDecoration(
                      color: const Color(0xE6191D1F),
                      border: Border.all(color: const Color(0xFF3A4246)),
                      borderRadius: BorderRadius.circular(5),
                    ),
                    child: Padding(
                      padding: const EdgeInsets.symmetric(
                        horizontal: 16,
                        vertical: 12,
                      ),
                      child: Text(
                        _loadState == VisualMapLoadState.loading
                            ? 'Loading local map data...'
                            : 'Map data unavailable\nBuild Visual Map on ROCK',
                        textAlign: TextAlign.center,
                        style: const TextStyle(
                          color: Color(0xFFD7DBDD),
                          fontSize: 12,
                          fontWeight: FontWeight.w700,
                        ),
                      ),
                    ),
                  ),
                ),
            ],
          );
        },
      ),
    );
  }

  NavigationCameraTransform _createTransform(Size size) {
    if (widget.viewMode == NavigationViewMode.overview &&
        widget.route.points.length >= 2) {
      return NavigationCameraTransform.overview(
        size: size,
        worldPoints: widget.route.points
            .map((point) => Offset(point.x, point.z))
            .toList(growable: false),
      );
    }
    return NavigationCameraTransform.follow(
      size: size,
      truckX: widget.displayX,
      truckZ: widget.displayZ,
      headingDegrees: widget.displayHeadingDegrees,
      zoom: widget.zoom,
      speedKph: widget.telemetry.speedKph,
      mode: widget.viewMode,
      screenOffset: widget.cameraOffset,
    );
  }

  VisualMapLevel? _selectLevel(VisualMapManifest? manifest, double scale) {
    if (manifest == null || manifest.levels.isEmpty) {
      return null;
    }
    final target = scale >= 0.075
        ? 2
        : scale >= 0.035
        ? 1
        : 0;
    return manifest.levels.firstWhere(
      (level) => level.level == target,
      orElse: () => manifest.levels.last,
    );
  }

  void _scheduleTiles(
    Size size,
    NavigationCameraTransform transform,
    VisualMapManifest manifest,
  ) {
    final level = _selectLevel(manifest, transform.scale);
    if (level == null) {
      return;
    }
    final corners = <Offset>[
      transform.unproject(Offset.zero),
      transform.unproject(Offset(size.width, 0)),
      transform.unproject(Offset(0, size.height)),
      transform.unproject(Offset(size.width, size.height)),
    ];
    final minX = corners.map((point) => point.dx).reduce(math.min);
    final maxX = corners.map((point) => point.dx).reduce(math.max);
    final minZ = corners.map((point) => point.dy).reduce(math.min);
    final maxZ = corners.map((point) => point.dy).reduce(math.max);
    final firstX = (minX / level.tileWorldSize).floor() - 1;
    final lastX = (maxX / level.tileWorldSize).floor() + 1;
    final firstY = (minZ / level.tileWorldSize).floor() - 1;
    final lastY = (maxZ / level.tileWorldSize).floor() + 1;
    final desired = <VisualMapTileKey>[];
    for (var x = firstX; x <= lastX; x++) {
      for (var y = firstY; y <= lastY; y++) {
        desired.add(VisualMapTileKey(level.level, x, y));
      }
    }
    if (desired.length > 64) {
      // Keep only the nearest tiles so rapid pans cannot flood decoding work.
      desired.sort((a, b) {
        final center = transform.unproject(
          Offset(size.width / 2, size.height / 2),
        );
        final adx = (a.x + 0.5) * level.tileWorldSize - center.dx;
        final ady = (a.y + 0.5) * level.tileWorldSize - center.dy;
        final bdx = (b.x + 0.5) * level.tileWorldSize - center.dx;
        final bdy = (b.y + 0.5) * level.tileWorldSize - center.dy;
        return (adx * adx + ady * ady).compareTo(bdx * bdx + bdy * bdy);
      });
      desired.removeRange(64, desired.length);
    }
    final signature = desired.map((key) => key.toString()).join('|');
    if (signature == _desiredSignature) {
      return;
    }
    _desiredSignature = signature;
    _loadTimer?.cancel();
    // Debounce camera motion to avoid requesting transient viewports.
    _loadTimer = Timer(
      const Duration(milliseconds: 80),
      () => unawaited(_loadTiles(desired, manifest)),
    );
  }

  Future<void> _loadTiles(
    List<VisualMapTileKey> desired,
    VisualMapManifest manifest,
  ) async {
    final repository = _repository;
    final game = _gameCode;
    final generation = _generation;
    if (repository == null || game.isEmpty) {
      return;
    }
    final next = <VisualMapTileKey, VisualMapTileData>{};
    final missing = <VisualMapTileKey>[];
    for (final key in desired) {
      if (_tiles[key] case final cached?) {
        next[key] = cached;
        continue;
      }
      missing.add(key);
    }
    for (var start = 0; start < missing.length; start += 8) {
      final batch = missing.sublist(start, math.min(start + 8, missing.length));
      try {
        final fetched = await Future.wait(
          batch.map(
            (key) async => MapEntry(
              key,
              await repository.fetchTile(
                game: game,
                manifest: manifest,
                key: key,
              ),
            ),
          ),
        );
        next.addEntries(fetched);
      } catch (_) {
        if (mounted && generation == _generation && next.isEmpty) {
          setState(() => _loadState = VisualMapLoadState.unavailable);
        }
        return;
      }
      if (!mounted || generation != _generation) {
        return;
      }
    }
    if (!mounted || generation != _generation) {
      return;
    }
    setState(() {
      _tiles = next;
      _paintTiles = next.values.toList(growable: false);
      _loadState = VisualMapLoadState.ready;
    });
  }
}

@visibleForTesting
class VisualMapPainter extends CustomPainter {
  const VisualMapPainter({
    required this.tiles,
    required this.transform,
    required this.detailLevel,
    this.darkMode = true,
    this.visiblePoiTypes,
  });

  final List<VisualMapTileData> tiles;
  final NavigationCameraTransform transform;
  final int detailLevel;
  final bool darkMode;
  final Set<String>? visiblePoiTypes;

  @override
  void paint(Canvas canvas, Size size) {
    final inverseScale = 1 / transform.scale;
    canvas
      ..save()
      ..clipRect(Offset.zero & size)
      ..translate(transform.screenOrigin.dx, transform.screenOrigin.dy)
      ..scale(transform.scale)
      ..rotate(transform.rotationRadians)
      ..translate(-transform.centerX, -transform.centerZ);

    final polygonIds = <int>{};
    for (final tile in tiles) {
      for (final polygon in tile.polygons) {
        if (!polygonIds.add(polygon.id) || polygon.points.length < 3) {
          continue;
        }
        final color = darkMode
            ? switch (polygon.color) {
                1 => const Color(0xFF142A34),
                2 => const Color(0xFF10232D),
                3 => const Color(0xFF102B2B),
                _ => const Color(0xFF132631),
              }
            : switch (polygon.color) {
                1 => const Color(0xFFD7E4E4),
                2 => const Color(0xFFE0E8E7),
                3 => const Color(0xFFD6E8DC),
                _ => const Color(0xFFDDE6E8),
              };
        canvas.drawPath(
          polygon.worldPath,
          Paint()
            ..color = color.withValues(alpha: polygon.drawOver ? 0.85 : 0.62),
        );
        if (detailLevel >= 2) {
          canvas.drawPath(
            polygon.worldPath,
            Paint()
              ..color = Color.lerp(
                color,
                darkMode ? const Color(0xFF6B8795) : const Color(0xFF8DA5AC),
                0.22,
              )!.withValues(alpha: 0.42)
              ..strokeWidth = 0.8 * inverseScale
              ..style = PaintingStyle.stroke,
          );
        }
      }
    }

    final lineIds = <int>{};
    final lines =
        tiles
            .expand((tile) => tile.lines)
            .where((line) => lineIds.add(line.id) && line.points.length >= 2)
            .toList(growable: false)
          ..sort((a, b) => _lineLayer(a).compareTo(_lineLayer(b)));
    for (final line in lines) {
      final path = line.worldPath;
      if (line.kind == 2 || line.kind == 3) {
        canvas.drawPath(
          path,
          Paint()
            ..color = darkMode
                ? const Color(0xFF071722)
                : const Color(0xFFC4D4DA)
            ..strokeWidth = 4.4 * inverseScale
            ..strokeCap = StrokeCap.round
            ..style = PaintingStyle.stroke,
        );
        _drawDashedPath(
          canvas,
          path,
          Paint()
            ..color = line.kind == 3
                ? const Color(0xFFA593E8)
                : const Color(0xFF35B7D4)
            ..strokeWidth = 2.2 * inverseScale
            ..strokeCap = StrokeCap.round
            ..style = PaintingStyle.stroke,
          dashLength: 8 * inverseScale,
          gapLength: 6 * inverseScale,
        );
        continue;
      }
      if (line.kind == 4) {
        if (detailLevel >= 2) {
          _drawDashedPath(
            canvas,
            path,
            Paint()
              ..color = darkMode
                  ? const Color(0x806F8998)
                  : const Color(0x80657982)
              ..strokeWidth = 0.8 * inverseScale
              ..strokeCap = StrokeCap.round
              ..style = PaintingStyle.stroke,
            dashLength: 4 * inverseScale,
            gapLength: 4 * inverseScale,
          );
        }
        continue;
      }

      final baseWidth = switch (line.roadClass) {
        3 => 5.8,
        2 => 4.8,
        1 => 3.4,
        _ => 2.1,
      };
      final worldWidth = line.widthMeters * transform.scale;
      final prefabBoost = line.kind == 1 ? 0.7 : 0.0;
      final width = math
          .max(baseWidth, worldWidth + prefabBoost)
          .clamp(1.9, 9.5);
      final innerColor = darkMode
          ? switch (line.roadClass) {
              3 => const Color(0xFF707984),
              2 => const Color(0xFF5B6570),
              1 => const Color(0xFF46515C),
              _ => const Color(0xFF333F4A),
            }
          : switch (line.roadClass) {
              3 => const Color(0xFF768D98),
              2 => const Color(0xFF8A9DA6),
              1 => const Color(0xFFA3B2B8),
              _ => const Color(0xFFB8C4C8),
            };
      canvas
        ..drawPath(
          path,
          Paint()
            ..color = darkMode
                ? const Color(0xFF07121A)
                : const Color(0xFFC9D5D9)
            ..strokeWidth = (width + 3) * inverseScale
            ..strokeCap = StrokeCap.round
            ..strokeJoin = StrokeJoin.round
            ..style = PaintingStyle.stroke,
        )
        ..drawPath(
          path,
          Paint()
            ..color = innerColor
            ..strokeWidth = width * inverseScale
            ..strokeCap = StrokeCap.round
            ..strokeJoin = StrokeJoin.round
            ..style = PaintingStyle.stroke,
        );
      if (line.roadClass >= 2 && detailLevel >= 2) {
        _drawDashedPath(
          canvas,
          path,
          Paint()
            ..color = darkMode
                ? const Color(0xB3C2D2DA)
                : const Color(0xD9F8FAFB)
            ..strokeWidth = 0.75 * inverseScale
            ..strokeCap = StrokeCap.round
            ..style = PaintingStyle.stroke,
          dashLength: 5 * inverseScale,
          gapLength: 5 * inverseScale,
        );
      } else if (line.kind == 1 && detailLevel >= 2) {
        canvas.drawPath(
          path,
          Paint()
            ..color = darkMode
                ? const Color(0x557F9CAC)
                : const Color(0x6692A5AD)
            ..strokeWidth = 0.7 * inverseScale
            ..strokeCap = StrokeCap.round
            ..style = PaintingStyle.stroke,
        );
      }
    }
    canvas.restore();

    final occupiedLabels = <Rect>[];
    final labelIds = <int>{};
    final labels =
        tiles
            .expand((tile) => tile.labels)
            .where((label) => labelIds.add(label.id))
            .toList(growable: false)
          ..sort((a, b) => b.priority.compareTo(a.priority));
    for (final label in labels) {
      final point = transform.project(label.position.dx, label.position.dy);
      if (!(Offset.zero & size).inflate(30).contains(point)) {
        continue;
      }
      final routeShield = _routeShieldPattern.hasMatch(label.text.trim());
      final painter = TextPainter(
        text: TextSpan(
          text: label.text,
          style: TextStyle(
            color: routeShield
                ? Colors.white
                : label.priority >= 2
                ? (darkMode ? const Color(0xFFE4F0F4) : const Color(0xFF233740))
                : (darkMode
                      ? const Color(0xFF93AAB6)
                      : const Color(0xFF526A74)),
            fontSize: routeShield
                ? 10
                : label.priority >= 2
                ? 15
                : 11,
            fontWeight: routeShield || label.priority >= 1
                ? FontWeight.w800
                : FontWeight.w600,
            shadows: <Shadow>[
              Shadow(
                color: darkMode ? Colors.black : Colors.white,
                blurRadius: 4,
              ),
            ],
          ),
        ),
        textDirection: TextDirection.ltr,
        maxLines: 1,
      )..layout(maxWidth: 150);
      final rect = Rect.fromLTWH(
        point.dx - painter.width / 2,
        point.dy - painter.height / 2,
        painter.width,
        painter.height,
      ).inflate(3);
      if (occupiedLabels.any((other) => other.overlaps(rect))) {
        continue;
      }
      occupiedLabels.add(rect);
      if (routeShield) {
        final shieldColor = label.text.trim().startsWith('E')
            ? const Color(0xFF2E6E2F)
            : const Color(0xFFA93228);
        canvas.drawRRect(
          RRect.fromRectAndRadius(rect, const Radius.circular(3)),
          Paint()
            ..color = shieldColor
            ..style = PaintingStyle.fill,
        );
        canvas.drawRRect(
          RRect.fromRectAndRadius(rect, const Radius.circular(3)),
          Paint()
            ..color = const Color(0xFFE8ECEE)
            ..strokeWidth = 0.9
            ..style = PaintingStyle.stroke,
        );
      }
      painter.paint(canvas, rect.topLeft + const Offset(3, 3));
    }

    if (detailLevel == 0) {
      return;
    }
    final poiIds = <int>{};
    for (final tile in tiles) {
      for (final poi in tile.pointsOfInterest) {
        if (!poiIds.add(poi.id)) {
          continue;
        }
        if (visiblePoiTypes case final types? when !types.contains(poi.type)) {
          continue;
        }
        final point = transform.project(poi.position.dx, poi.position.dy);
        if (!(Offset.zero & size).deflate(30).contains(point)) {
          continue;
        }
        _paintPoi(
          canvas,
          point,
          poi.type,
          label: poi.label,
          emphasized: visiblePoiTypes != null,
        );
      }
    }
  }

  static int _lineLayer(VisualMapLine line) {
    if (line.kind == 2 || line.kind == 3) {
      return 0;
    }
    if (line.kind == 4) {
      return 30;
    }
    return 5 + line.roadClass + (line.kind == 1 ? 10 : 0);
  }

  static final RegExp _routeShieldPattern = RegExp(r'^[A-Z]{1,2}\d{1,3}$');

  static void _drawDashedPath(
    Canvas canvas,
    Path path,
    Paint paint, {
    double dashLength = 8,
    double gapLength = 6,
  }) {
    for (final metric in path.computeMetrics()) {
      for (
        var offset = 0.0;
        offset < metric.length;
        offset += dashLength + gapLength
      ) {
        canvas.drawPath(
          metric.extractPath(
            offset,
            math.min(offset + dashLength, metric.length),
          ),
          paint,
        );
      }
    }
  }

  static void _paintPoi(
    Canvas canvas,
    Offset point,
    String type, {
    String? label,
    required bool emphasized,
  }) {
    final (color, icon) = _poiVisual(type);
    final markerRect = Rect.fromCenter(
      center: point,
      width: emphasized ? 29 : 25,
      height: emphasized ? 29 : 25,
    );
    final marker = Path()
      ..addRRect(RRect.fromRectAndRadius(markerRect, const Radius.circular(6)));
    canvas
      ..drawShadow(marker, const Color(0xCC000000), 5, false)
      ..drawPath(marker, Paint()..color = color)
      ..drawPath(
        marker,
        Paint()
          ..color = const Color(0x80FFFFFF)
          ..strokeWidth = 0.8
          ..style = PaintingStyle.stroke,
      );
    final painter = TextPainter(
      text: TextSpan(
        text: String.fromCharCode(icon.codePoint),
        style: TextStyle(
          color: Colors.white,
          fontFamily: icon.fontFamily,
          package: icon.fontPackage,
          fontSize: emphasized ? 17 : 15,
        ),
      ),
      textDirection: TextDirection.ltr,
    )..layout();
    painter.paint(
      canvas,
      markerRect.center - Offset(painter.width / 2, painter.height / 2),
    );

    if (emphasized && label != null && label.trim().isNotEmpty) {
      _paintPoiLabel(canvas, point + const Offset(18, -12), label.trim());
    }
  }

  static (Color, IconData) _poiVisual(String type) => switch (type) {
    'Fuel' => (const Color(0xFF1759A5), Icons.local_gas_station_rounded),
    'Rest' => (const Color(0xFF2867B4), Icons.hotel_rounded),
    'Service' => (const Color(0xFF4E5C68), Icons.build_rounded),
    'Garage' => (const Color(0xFF4E5C68), Icons.garage_rounded),
    'Dealer' => (const Color(0xFF4E5C68), Icons.local_shipping_rounded),
    'Ferry' => (const Color(0xFF238AB7), Icons.directions_boat_filled_rounded),
    'Train' => (const Color(0xFF7966C5), Icons.train_rounded),
    'WeighStation' => (const Color(0xFF579D62), Icons.monitor_weight_rounded),
    'Company' => (const Color(0xFFCC7D39), Icons.factory_rounded),
    'Recruitment' => (const Color(0xFF9C6BC7), Icons.badge_rounded),
    _ => (const Color(0xFF758E9B), Icons.place_rounded),
  };

  static void _paintPoiLabel(Canvas canvas, Offset origin, String label) {
    final painter = TextPainter(
      text: TextSpan(
        text: label,
        style: const TextStyle(
          color: Color(0xFFE8F3F6),
          fontSize: 10,
          fontWeight: FontWeight.w800,
        ),
      ),
      textDirection: TextDirection.ltr,
      maxLines: 1,
      ellipsis: '...',
    )..layout(maxWidth: 130);
    final rect = Rect.fromLTWH(
      origin.dx,
      origin.dy,
      painter.width + 12,
      painter.height + 7,
    );
    canvas.drawRRect(
      RRect.fromRectAndRadius(rect, const Radius.circular(7)),
      Paint()..color = const Color(0xEB091923),
    );
    painter.paint(canvas, rect.topLeft + const Offset(6, 3.5));
  }

  @override
  bool shouldRepaint(covariant VisualMapPainter oldDelegate) =>
      oldDelegate.tiles != tiles ||
      oldDelegate.transform.centerX != transform.centerX ||
      oldDelegate.transform.centerZ != transform.centerZ ||
      oldDelegate.transform.scale != transform.scale ||
      oldDelegate.transform.rotationRadians != transform.rotationRadians ||
      oldDelegate.detailLevel != detailLevel ||
      oldDelegate.darkMode != darkMode ||
      oldDelegate.visiblePoiTypes != visiblePoiTypes;
}
