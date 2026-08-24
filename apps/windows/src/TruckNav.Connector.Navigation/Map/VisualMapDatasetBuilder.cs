using System.Text.Json;
using TruckNav.Connector.Core.Enums;
using TruckNav.Connector.Core.Models;
using TruckNav.Connector.Navigation.Data;

namespace TruckNav.Connector.Navigation.Map;

internal sealed class VisualMapDatasetBuilder
{
    public const string GeneratorVersion = "trucknav-vector-1";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly NavigationStoragePaths _navigationPaths;

    public VisualMapDatasetBuilder(NavigationStoragePaths navigationPaths)
    {
        _navigationPaths = navigationPaths;
    }

    public async Task<VisualMapDataset> BuildAsync(
        GameInstallation installation,
        IProgress<VisualMapBuildProgress> progress,
        CancellationToken cancellationToken)
    {
        var game = installation.Game;
        var map = NavigationStoragePaths.GetMapCode(game);
        var parser = _navigationPaths.GetParserDirectory(game);
        string Source(string suffix) => Path.Combine(parser, $"{map}-{suffix}.json");

        var requiredFiles = new[]
        {
            Source("nodes"), Source("roads"), Source("roadLooks"), Source("prefabs"),
            Source("prefabDescriptions"), Source("mapAreas"), Source("ferries"),
            Source("cities"), Source("pois"), Source("dividers")
        };
        var missing = requiredFiles.FirstOrDefault(path => !File.Exists(path));
        if (missing is not null)
        {
            throw new InvalidOperationException($"Parser output is incomplete: {Path.GetFileName(missing)} is missing.");
        }

        Report(progress, game, "Reading map sectors...", 0.03, "Reading installed-game visual sources");
        var roadsTask = ReadArrayAsync<ParserRoad>(Source("roads"), cancellationToken);
        var prefabsTask = ReadArrayAsync<ParserPrefab>(Source("prefabs"), cancellationToken);
        var areasTask = ReadArrayAsync<ParserMapArea>(Source("mapAreas"), cancellationToken);
        var dividersTask = ReadArrayAsync<ParserDivider>(Source("dividers"), cancellationToken);
        await Task.WhenAll(roadsTask, prefabsTask, areasTask, dividersTask);
        var roads = roadsTask.Result;
        var prefabs = prefabsTask.Result;
        var areas = areasTask.Result;
        var dividers = dividersTask.Result;

        var requiredNodeUids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var road in roads)
        {
            requiredNodeUids.Add(road.StartNodeUid);
            requiredNodeUids.Add(road.EndNodeUid);
        }
        foreach (var prefab in prefabs)
        {
            foreach (var uid in prefab.NodeUids)
            {
                requiredNodeUids.Add(uid);
            }
        }
        foreach (var area in areas)
        {
            foreach (var uid in area.NodeUids)
            {
                requiredNodeUids.Add(uid);
            }
        }
        foreach (var divider in dividers)
        {
            requiredNodeUids.Add(divider.StartNodeUid);
            requiredNodeUids.Add(divider.EndNodeUid);
        }

        Report(progress, game, "Reading roads...", 0.17, $"Resolving {requiredNodeUids.Count:N0} visual nodes");
        var nodes = await ReadRequiredNodesAsync(Source("nodes"), requiredNodeUids, cancellationToken);
        var roadLooks = (await ReadArrayAsync<ParserRoadLook>(Source("roadLooks"), cancellationToken))
            .ToDictionary(item => item.Token, StringComparer.Ordinal);

        Report(progress, game, "Reading prefabs...", 0.32, $"Reading {prefabs.Count:N0} prefab placements");
        var usedPrefabTokens = prefabs.Select(item => item.Token).ToHashSet(StringComparer.Ordinal);
        var prefabDescriptions = (await ReadArrayAsync<ParserPrefabDescription>(Source("prefabDescriptions"), cancellationToken))
            .Where(item => usedPrefabTokens.Contains(item.Token))
            .ToDictionary(item => item.Token, StringComparer.Ordinal);

        Report(progress, game, "Generating map geometry...", 0.45, "Preserving curved roads and prefab junction paths");
        var lines = new List<VisualLineFeature>(roads.Count + prefabs.Count * 3 + dividers.Count);
        var polygons = new List<VisualPolygonFeature>(areas.Count + prefabs.Count);
        var nextId = 1;
        var roadCount = AddRoads(roads, roadLooks, nodes, lines, ref nextId);
        var prefabRoadCount = AddPrefabs(prefabs, prefabDescriptions, nodes, lines, polygons, ref nextId);
        var mapAreaCount = AddMapAreas(areas, nodes, polygons, ref nextId);
        AddDividers(dividers, nodes, lines, ref nextId);

        Report(progress, game, "Indexing ferries and cities...", 0.67, "Adding ferry, train, label, and POI context");
        var ferries = await ReadArrayAsync<ParserFerry>(Source("ferries"), cancellationToken);
        var ferryCount = AddFerries(ferries, lines, ref nextId);
        var cities = await ReadArrayAsync<ParserCity>(Source("cities"), cancellationToken);
        var labels = cities
            .Where(city => !string.IsNullOrWhiteSpace(city.Name))
            .Select(city => new VisualMapLabel(
                nextId++,
                city.Name,
                new WorldPoint(city.X, city.Y),
                city.Population >= 500_000 ? (byte)2 : city.Population >= 100_000 ? (byte)1 : (byte)0))
            .ToArray();

        Report(progress, game, "Indexing POIs...", 0.77, "Classifying verified simulator points of interest");
        var parserPois = await ReadArrayAsync<ParserPoi>(Source("pois"), cancellationToken);
        var pois = parserPois
            .Select(poi => new VisualMapPoi(nextId++, ClassifyPoi(poi), new WorldPoint(poi.X, poi.Y), poi.Label))
            .ToArray();

        var allPoints = lines.SelectMany(line => line.Points)
            .Concat(polygons.SelectMany(polygon => polygon.Points))
            .Concat(labels.Select(label => label.Position));
        var bounds = VisualMapGeometry.GetBounds(allPoints.ToArray());
        var manifest = new VisualMapManifest(
            game,
            installation.Version,
            GeneratorVersion,
            DateTimeOffset.UtcNow,
            installation.DlcSignature,
            bounds.MinX,
            bounds.MinZ,
            bounds.MaxX,
            bounds.MaxZ,
            VisualMapService.Levels);

        Report(progress, game, "Building tiles...", 0.88, "Writing the compact vector feature index");
        return new VisualMapDataset(
            manifest,
            lines,
            polygons,
            labels,
            pois,
            roadCount,
            prefabRoadCount,
            mapAreaCount,
            ferryCount);
    }

    private static int AddRoads(
        IReadOnlyList<ParserRoad> roads,
        IReadOnlyDictionary<string, ParserRoadLook> roadLooks,
        IReadOnlyDictionary<string, ParserNode> nodes,
        ICollection<VisualLineFeature> output,
        ref int nextId)
    {
        var count = 0;
        foreach (var road in roads)
        {
            if (road.Hidden || !nodes.TryGetValue(road.StartNodeUid, out var start) ||
                !nodes.TryGetValue(road.EndNodeUid, out var end))
            {
                continue;
            }

            roadLooks.TryGetValue(road.RoadLookToken, out var look);
            var points = CreateHermiteSpline(start, end);
            var roadClass = ClassifyRoad(look);
            var width = CalculateRoadWidth(look);
            var separation = road.MaybeDivided ? look?.LaneOffset ?? 0 : look?.Offset ?? 0;
            if (Math.Abs(separation) >= 1)
            {
                var half = Math.Abs(separation) / 2 + Math.Max(2.5, width * 0.22);
                output.Add(new VisualLineFeature(nextId++, VisualLineKind.Road, roadClass, width / 2, OffsetLine(points, half)));
                output.Add(new VisualLineFeature(nextId++, VisualLineKind.Road, roadClass, width / 2, OffsetLine(points, -half)));
            }
            else
            {
                output.Add(new VisualLineFeature(nextId++, VisualLineKind.Road, roadClass, width, points));
            }
            count++;
        }

        return count;
    }

    private static int AddPrefabs(
        IReadOnlyList<ParserPrefab> prefabs,
        IReadOnlyDictionary<string, ParserPrefabDescription> descriptions,
        IReadOnlyDictionary<string, ParserNode> nodes,
        ICollection<VisualLineFeature> lines,
        ICollection<VisualPolygonFeature> polygons,
        ref int nextId)
    {
        var roadCount = 0;
        foreach (var prefab in prefabs)
        {
            if (prefab.Hidden || prefab.NodeUids.Count == 0 ||
                !nodes.TryGetValue(prefab.NodeUids[0], out var originNode) ||
                !descriptions.TryGetValue(prefab.Token, out var description) ||
                description.Nodes.Count == 0 || description.MapPoints.Count == 0)
            {
                continue;
            }

            var originIndex = Math.Clamp(prefab.OriginNodeIndex, 0, description.Nodes.Count - 1);
            var localOrigin = description.Nodes[originIndex];
            var rotation = originNode.Rotation - localOrigin.Rotation;
            WorldPoint Transform(ParserMapPoint point) => TransformPrefabPoint(point.X, point.Y, localOrigin, originNode, rotation);

            var seenEdges = new HashSet<long>();
            for (var index = 0; index < description.MapPoints.Count; index++)
            {
                var point = description.MapPoints[index];
                if (!string.Equals(point.Type, "road", StringComparison.OrdinalIgnoreCase) || point.Hidden)
                {
                    continue;
                }
                foreach (var neighborIndex in point.Neighbors)
                {
                    if ((uint)neighborIndex >= description.MapPoints.Count || neighborIndex == index)
                    {
                        continue;
                    }
                    var neighbor = description.MapPoints[neighborIndex];
                    if (!string.Equals(neighbor.Type, "road", StringComparison.OrdinalIgnoreCase) || neighbor.Hidden)
                    {
                        continue;
                    }
                    var low = Math.Min(index, neighborIndex);
                    var high = Math.Max(index, neighborIndex);
                    if (!seenEdges.Add(((long)low << 32) | (uint)high))
                    {
                        continue;
                    }

                    var lanes = Math.Max(1, LaneCount(point.LanesLeft) + LaneCount(point.LanesRight));
                    lines.Add(new VisualLineFeature(
                        nextId++,
                        VisualLineKind.PrefabRoad,
                        lanes >= 4 ? VisualRoadClass.Major : VisualRoadClass.Local,
                        (float)Math.Max(6, lanes * 4.5 + Math.Abs(point.Offset)),
                        [Transform(point), Transform(neighbor)]));
                    roadCount++;
                }
            }

            foreach (var path in TracePrefabPolygons(description.MapPoints))
            {
                var transformed = path.Select(index => Transform(description.MapPoints[index])).ToArray();
                if (transformed.Length >= 3)
                {
                    var source = description.MapPoints[path[0]];
                    polygons.Add(new VisualPolygonFeature(nextId++, source.Color, source.RoadOver, transformed));
                }
            }
        }

        return roadCount;
    }

    private static int AddMapAreas(
        IReadOnlyList<ParserMapArea> areas,
        IReadOnlyDictionary<string, ParserNode> nodes,
        ICollection<VisualPolygonFeature> output,
        ref int nextId)
    {
        var count = 0;
        foreach (var area in areas)
        {
            var points = area.NodeUids
                .Select(uid => nodes.TryGetValue(uid, out var node) ? new WorldPoint(node.X, node.Y) : (WorldPoint?)null)
                .Where(point => point.HasValue)
                .Select(point => point!.Value)
                .ToArray();
            if (points.Length < 3)
            {
                continue;
            }
            output.Add(new VisualPolygonFeature(nextId++, area.Color, area.DrawOver, points));
            count++;
        }
        return count;
    }

    private static void AddDividers(
        IReadOnlyList<ParserDivider> dividers,
        IReadOnlyDictionary<string, ParserNode> nodes,
        ICollection<VisualLineFeature> output,
        ref int nextId)
    {
        foreach (var divider in dividers)
        {
            if (!nodes.TryGetValue(divider.StartNodeUid, out var start) ||
                !nodes.TryGetValue(divider.EndNodeUid, out var end))
            {
                continue;
            }
            output.Add(new VisualLineFeature(
                nextId++, VisualLineKind.Divider, VisualRoadClass.Minor, 0.8f, CreateHermiteSpline(start, end)));
        }
    }

    private static int AddFerries(IReadOnlyList<ParserFerry> ferries, ICollection<VisualLineFeature> output, ref int nextId)
    {
        var pairs = new HashSet<string>(StringComparer.Ordinal);
        var count = 0;
        foreach (var ferry in ferries)
        {
            foreach (var connection in ferry.Connections)
            {
                var pair = string.CompareOrdinal(ferry.Token, connection.Token) <= 0
                    ? $"{ferry.Token}|{connection.Token}"
                    : $"{connection.Token}|{ferry.Token}";
                if (!pairs.Add(pair))
                {
                    continue;
                }
                var points = new List<WorldPoint> { new(ferry.X, ferry.Y) };
                points.AddRange(connection.IntermediatePoints.Select(point => new WorldPoint(point.X, point.Y)));
                points.Add(new WorldPoint(connection.X, connection.Y));
                output.Add(new VisualLineFeature(
                    nextId++, ferry.Train ? VisualLineKind.Train : VisualLineKind.Ferry,
                    VisualRoadClass.Major, 2, points));
                count++;
            }
        }
        return count;
    }

    private static IReadOnlyList<WorldPoint> CreateHermiteSpline(ParserNode start, ParserNode end)
    {
        var angleDelta = start.Rotation - end.Rotation;
        var steps = Math.Min(8, (int)Math.Floor(Math.Abs(Math.Tan(angleDelta)) * 20) + 1);
        steps = Math.Max(1, steps);
        var distance = Math.Sqrt(Math.Pow(end.X - start.X, 2) + Math.Pow(end.Y - start.Y, 2));
        var tangentStart = new WorldPoint(Math.Cos(start.Rotation) * distance, Math.Sin(start.Rotation) * distance);
        var tangentEnd = new WorldPoint(Math.Cos(end.Rotation) * distance, Math.Sin(end.Rotation) * distance);
        var result = new WorldPoint[steps + 1];
        for (var index = 0; index <= steps; index++)
        {
            var t = index / (double)steps;
            var t2 = t * t;
            var t3 = t2 * t;
            var h00 = 2 * t3 - 3 * t2 + 1;
            var h10 = t3 - 2 * t2 + t;
            var h01 = -2 * t3 + 3 * t2;
            var h11 = t3 - t2;
            result[index] = new WorldPoint(
                start.X * h00 + tangentStart.X * h10 + end.X * h01 + tangentEnd.X * h11,
                start.Y * h00 + tangentStart.Z * h10 + end.Y * h01 + tangentEnd.Z * h11);
        }
        return result;
    }

    private static IReadOnlyList<WorldPoint> OffsetLine(IReadOnlyList<WorldPoint> points, double offset)
    {
        var output = new WorldPoint[points.Count];
        for (var index = 0; index < points.Count; index++)
        {
            var previous = points[Math.Max(0, index - 1)];
            var next = points[Math.Min(points.Count - 1, index + 1)];
            var dx = next.X - previous.X;
            var dz = next.Z - previous.Z;
            var length = Math.Sqrt(dx * dx + dz * dz);
            output[index] = length < 0.001
                ? points[index]
                : new WorldPoint(points[index].X - dz / length * offset, points[index].Z + dx / length * offset);
        }
        return output;
    }

    private static WorldPoint TransformPrefabPoint(
        double x,
        double y,
        ParserPrefabNode localOrigin,
        ParserNode worldOrigin,
        double rotation)
    {
        var dx = x - localOrigin.X;
        var dy = y - localOrigin.Y;
        var cosine = Math.Cos(rotation);
        var sine = Math.Sin(rotation);
        return new WorldPoint(
            worldOrigin.X + dx * cosine - dy * sine,
            worldOrigin.Y + dx * sine + dy * cosine);
    }

    private static IReadOnlyList<IReadOnlyList<int>> TracePrefabPolygons(IReadOnlyList<ParserMapPoint> points)
    {
        var output = new List<IReadOnlyList<int>>();
        var visited = new HashSet<int>();
        for (var start = 0; start < points.Count; start++)
        {
            if (visited.Contains(start) || !string.Equals(points[start].Type, "polygon", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            var component = new List<int>();
            var queue = new Queue<int>();
            queue.Enqueue(start);
            visited.Add(start);
            while (queue.TryDequeue(out var current))
            {
                component.Add(current);
                foreach (var neighbor in points[current].Neighbors)
                {
                    if ((uint)neighbor < points.Count &&
                        string.Equals(points[neighbor].Type, "polygon", StringComparison.OrdinalIgnoreCase) &&
                        visited.Add(neighbor))
                    {
                        queue.Enqueue(neighbor);
                    }
                }
            }

            if (component.Count >= 3)
            {
                var centerX = component.Average(index => points[index].X);
                var centerY = component.Average(index => points[index].Y);
                output.Add(component
                    .OrderBy(index => Math.Atan2(points[index].Y - centerY, points[index].X - centerX))
                    .ToArray());
            }
        }
        return output;
    }

    private static VisualRoadClass ClassifyRoad(ParserRoadLook? look)
    {
        if (look is null)
        {
            return VisualRoadClass.Local;
        }
        var lanes = look.LanesLeft.Count + look.LanesRight.Count;
        var name = look.Name.ToLowerInvariant();
        if (lanes >= 4 || name.Contains("motorway") || name.Contains("freeway") || name.Contains("autobahn"))
        {
            return VisualRoadClass.Motorway;
        }
        if (lanes >= 3 || name.Contains("highway") || name.Contains("express"))
        {
            return VisualRoadClass.Major;
        }
        return lanes >= 2 ? VisualRoadClass.Local : VisualRoadClass.Minor;
    }

    private static float CalculateRoadWidth(ParserRoadLook? look)
    {
        if (look is null)
        {
            return 8;
        }
        var laneCount = Math.Max(1, look.LanesLeft.Count + look.LanesRight.Count);
        return (float)Math.Clamp(
            laneCount * 4.5 + look.ShoulderSpaceLeft + look.ShoulderSpaceRight + Math.Abs(look.Offset ?? 0),
            5,
            36);
    }

    private static int LaneCount(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.Number when value.TryGetInt32(out var count) => Math.Max(0, count),
        JsonValueKind.String when int.TryParse(value.GetString(), out var count) => Math.Max(0, count),
        JsonValueKind.String when string.Equals(value.GetString(), "auto", StringComparison.OrdinalIgnoreCase) => 1,
        _ => 0
    };

    private static string ClassifyPoi(ParserPoi poi)
    {
        var value = $"{poi.Type} {poi.Icon}".ToLowerInvariant();
        if (value.Contains("gas") || value.Contains("fuel")) return "Fuel";
        if (value.Contains("parking") || value.Contains("hotel") || value.Contains("rest")) return "Rest";
        if (value.Contains("service")) return "Service";
        if (value.Contains("garage")) return "Garage";
        if (value.Contains("dealer")) return "Dealer";
        if (value.Contains("recruit")) return "Recruitment";
        if (value.Contains("ferry")) return "Ferry";
        if (value.Contains("train")) return "Train";
        if (value.Contains("weigh")) return "WeighStation";
        if (value.Contains("company")) return "Company";
        return "Other";
    }

    private static async Task<IReadOnlyList<T>> ReadArrayAsync<T>(string path, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 128, useAsync: true);
        return await JsonSerializer.DeserializeAsync<List<T>>(stream, JsonOptions, cancellationToken) ?? [];
    }

    private static async Task<IReadOnlyDictionary<string, ParserNode>> ReadRequiredNodesAsync(
        string path,
        IReadOnlySet<string> requiredUids,
        CancellationToken cancellationToken)
    {
        var output = new Dictionary<string, ParserNode>(requiredUids.Count, StringComparer.Ordinal);
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 256, useAsync: true);
        await foreach (var node in JsonSerializer.DeserializeAsyncEnumerable<ParserNode>(stream, JsonOptions, cancellationToken))
        {
            if (node is not null && requiredUids.Contains(node.Uid))
            {
                output[node.Uid] = node;
            }
        }
        return output;
    }

    private static void Report(
        IProgress<VisualMapBuildProgress> progress,
        GameType game,
        string stage,
        double fraction,
        string message) =>
        progress.Report(new VisualMapBuildProgress(game, stage, fraction, message));
}
