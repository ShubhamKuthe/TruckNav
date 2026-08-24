using System.Text.Json;
using TruckNav.Connector.Core.Enums;
using TruckNav.Connector.Core.Models;

namespace TruckNav.Connector.Navigation.Data;

public sealed class TruckermudgeonGraphImporter
{
    public const string GeneratorCommit = "d56d0e3fb319230e84284f3029f8bda2c4b572a2";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<RoadGraph> ImportAsync(
        GameInstallation installation,
        string parserDirectory,
        string generatedDirectory,
        CancellationToken cancellationToken = default)
    {
        var mapCode = NavigationStoragePaths.GetMapCode(installation.Game);
        var graphPath = Path.Combine(generatedDirectory, $"{mapCode}-graph.json");
        var nodesPath = Path.Combine(parserDirectory, $"{mapCode}-nodes.json");
        ValidateRequiredFiles(
            graphPath,
            nodesPath,
            Path.Combine(parserDirectory, $"{mapCode}-companies.json"),
            Path.Combine(parserDirectory, $"{mapCode}-cities.json"),
            Path.Combine(parserDirectory, $"{mapCode}-companyDefs.json"));

        await using var graphStream = new FileStream(
            graphPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 1024 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        using var graphDocument = await JsonDocument.ParseAsync(graphStream, cancellationToken: cancellationToken);
        var graphEntries = graphDocument.RootElement.GetProperty("graph");

        var sourceIds = new List<string>(graphEntries.GetArrayLength());
        var nodeIndexes = new Dictionary<string, int>(graphEntries.GetArrayLength(), StringComparer.OrdinalIgnoreCase);
        foreach (var entry in graphEntries.EnumerateArray())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var sourceId = entry[0].GetString() ?? throw new InvalidDataException("Graph node is missing its source id.");
            AddNode(sourceId, sourceIds, nodeIndexes);
            var neighbors = entry[1];
            AddNeighborIds(neighbors, "forward", sourceIds, nodeIndexes);
            AddNeighborIds(neighbors, "backward", sourceIds, nodeIndexes);
        }

        var positions = new WorldPoint[sourceIds.Count];
        var positionFound = new bool[sourceIds.Count];
        await foreach (var node in ReadArrayAsync<NodeImport>(nodesPath, cancellationToken))
        {
            if (node?.Uid is not null && nodeIndexes.TryGetValue(node.Uid, out var index))
            {
                positions[index] = new WorldPoint(node.X, node.Y);
                positionFound[index] = true;
            }
        }

        if (positionFound.Any(found => !found))
        {
            throw new InvalidDataException("The generated graph references nodes that are absent from parser output.");
        }

        var forwardEdges = CreateEdgeBuckets(sourceIds.Count);
        var backwardEdges = CreateEdgeBuckets(sourceIds.Count);
        foreach (var entry in graphEntries.EnumerateArray())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var sourceIndex = nodeIndexes[entry[0].GetString()!];
            var neighbors = entry[1];
            ReadEdges(neighbors, "forward", forwardEdges[sourceIndex], nodeIndexes);
            ReadEdges(neighbors, "backward", backwardEdges[sourceIndex], nodeIndexes);
        }

        var nodes = new RoadNode[sourceIds.Count];
        for (var index = 0; index < nodes.Length; index++)
        {
            nodes[index] = new RoadNode(index, sourceIds[index], positions[index], forwardEdges[index], backwardEdges[index]);
        }

        var cities = await ReadNamesAsync(Path.Combine(parserDirectory, $"{mapCode}-cities.json"), cancellationToken);
        var companies = await ReadNamesAsync(Path.Combine(parserDirectory, $"{mapCode}-companyDefs.json"), cancellationToken);
        var destinations = new List<RoadDestination>();
        await foreach (var company in ReadArrayAsync<CompanyImport>(
                           Path.Combine(parserDirectory, $"{mapCode}-companies.json"),
                           cancellationToken))
        {
            if (company?.NodeUid is null ||
                company.CityToken is null ||
                company.Token is null ||
                !nodeIndexes.TryGetValue(company.NodeUid, out var nodeIndex))
            {
                continue;
            }

            destinations.Add(new RoadDestination(
                company.CityToken,
                company.Token,
                cities.GetValueOrDefault(company.CityToken),
                companies.GetValueOrDefault(company.Token),
                nodeIndex,
                new WorldPoint(company.X, company.Y)));
        }

        var pointsOfInterest = await ReadPointsOfInterestAsync(
            Path.Combine(parserDirectory, $"{mapCode}-pois.json"),
            cancellationToken);

        var metadata = new RoadGraphMetadata(
            installation.Game,
            installation.Version,
            "truckermudgeon/maps external generator",
            GeneratorCommit,
            FormatVersion: 2,
            GeneratedUtc: DateTimeOffset.UtcNow,
            SourceGamePath: installation.InstallPath,
            DlcSignature: installation.DlcSignature);
        return new RoadGraph(metadata, nodes, destinations, pointsOfInterest);
    }

    private static List<RoadEdge>[] CreateEdgeBuckets(int count)
    {
        var result = new List<RoadEdge>[count];
        for (var index = 0; index < count; index++)
        {
            result[index] = [];
        }

        return result;
    }

    private static void AddNeighborIds(
        JsonElement neighbors,
        string propertyName,
        List<string> sourceIds,
        Dictionary<string, int> nodeIndexes)
    {
        if (!neighbors.TryGetProperty(propertyName, out var edges))
        {
            return;
        }

        foreach (var edge in edges.EnumerateArray())
        {
            var targetId = edge.GetProperty("nodeUid").GetString();
            if (targetId is not null)
            {
                AddNode(targetId, sourceIds, nodeIndexes);
            }
        }
    }

    private static void AddNode(string sourceId, List<string> sourceIds, Dictionary<string, int> nodeIndexes)
    {
        if (nodeIndexes.ContainsKey(sourceId))
        {
            return;
        }

        nodeIndexes.Add(sourceId, sourceIds.Count);
        sourceIds.Add(sourceId);
    }

    private static void ReadEdges(
        JsonElement neighbors,
        string propertyName,
        List<RoadEdge> output,
        IReadOnlyDictionary<string, int> nodeIndexes)
    {
        if (!neighbors.TryGetProperty(propertyName, out var edges))
        {
            return;
        }

        foreach (var edge in edges.EnumerateArray())
        {
            var targetId = edge.GetProperty("nodeUid").GetString()!;
            var direction = edge.GetProperty("direction").GetString() == "backward"
                ? RoadTravelDirection.Backward
                : RoadTravelDirection.Forward;
            output.Add(new RoadEdge(
                nodeIndexes[targetId],
                edge.GetProperty("distance").GetSingle(),
                edge.GetProperty("duration").GetSingle(),
                direction,
                edge.TryGetProperty("isOneLaneRoad", out var oneLane) && oneLane.GetBoolean(),
                edge.TryGetProperty("isFerry", out var ferry) && ferry.GetBoolean(),
                edge.TryGetProperty("dlcGuard", out var guard) ? guard.GetInt32() : 0));
        }
    }

    private static async Task<Dictionary<string, string>> ReadNamesAsync(
        string path,
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        await foreach (var item in ReadArrayAsync<NameImport>(path, cancellationToken))
        {
            if (item?.Token is null)
            {
                continue;
            }

            var name = string.IsNullOrWhiteSpace(item.NameLocalized) ? item.Name : item.NameLocalized;
            if (!string.IsNullOrWhiteSpace(name))
            {
                result[item.Token] = name;
            }
        }

        return result;
    }

    internal static async Task<IReadOnlyList<RoadPointOfInterest>> ReadPointsOfInterestAsync(
        string path,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            return [];
        }

        var result = new List<RoadPointOfInterest>();
        var occupiedCells = new HashSet<(NavigationPoiType Type, int X, int Z)>();
        await foreach (var item in ReadArrayAsync<PoiImport>(path, cancellationToken))
        {
            if (item?.Icon is null || MapPoiType(item.Icon) is not { } type)
            {
                continue;
            }

            // Parser output can contain several parking/gas triggers in one prefab.
            var key = (type, (int)Math.Round(item.X / 40), (int)Math.Round(item.Y / 40));
            if (occupiedCells.Add(key))
            {
                result.Add(new RoadPointOfInterest(type, new WorldPoint(item.X, item.Y)));
            }
        }

        return result;
    }

    private static NavigationPoiType? MapPoiType(string icon)
    {
        if (icon.Equals("gas_ico", StringComparison.OrdinalIgnoreCase))
        {
            return NavigationPoiType.Fuel;
        }

        if (icon.Equals("parking_ico", StringComparison.OrdinalIgnoreCase))
        {
            return NavigationPoiType.Rest;
        }

        if (icon.Equals("service_ico", StringComparison.OrdinalIgnoreCase))
        {
            return NavigationPoiType.Service;
        }

        if (icon.Equals("garage_large_ico", StringComparison.OrdinalIgnoreCase))
        {
            return NavigationPoiType.Garage;
        }

        if (icon.Equals("dealer_ico", StringComparison.OrdinalIgnoreCase) ||
            icon.EndsWith("_dlr", StringComparison.OrdinalIgnoreCase))
        {
            return NavigationPoiType.Dealer;
        }

        if (icon.Equals("port_overlay", StringComparison.OrdinalIgnoreCase) ||
            icon.Equals("shuttle", StringComparison.OrdinalIgnoreCase))
        {
            return NavigationPoiType.Ferry;
        }

        if (icon.Equals("weigh_ico", StringComparison.OrdinalIgnoreCase) ||
            icon.Equals("weigh_station_ico", StringComparison.OrdinalIgnoreCase))
        {
            return NavigationPoiType.WeighStation;
        }

        return null;
    }

    private static async IAsyncEnumerable<T?> ReadArrayAsync<T>(
        string path,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 1024 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        await foreach (var item in JsonSerializer.DeserializeAsyncEnumerable<T>(
                           stream,
                           JsonOptions,
                           cancellationToken))
        {
            yield return item;
        }
    }

    private static void ValidateRequiredFiles(params string[] paths)
    {
        var missing = paths.Where(path => !File.Exists(path)).ToArray();
        if (missing.Length > 0)
        {
            throw new FileNotFoundException($"Navigation generation output is incomplete: {string.Join(", ", missing)}");
        }
    }

    private sealed record NodeImport(string? Uid, double X, double Y);

    private sealed record CompanyImport(
        string? Token,
        string? CityToken,
        string? NodeUid,
        double X,
        double Y);

    private sealed record NameImport(string? Token, string? Name, string? NameLocalized);

    private sealed record PoiImport(double X, double Y, string? Icon);
}
