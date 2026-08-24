using System.Collections.Concurrent;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using TruckNav.Connector.Core.Enums;
using TruckNav.Connector.Core.Interfaces;
using TruckNav.Connector.Core.Models;

namespace TruckNav.Connector.Navigation.Map;

internal sealed class VisualMapService : IVisualMapService
{
    public static IReadOnlyList<VisualMapLevel> Levels { get; } =
    [
        new(0, 32_768, "overview"),
        new(1, 8_192, "normal"),
        new(2, 2_048, "close")
    ];

    private readonly IGameInstallationLocator _installationLocator;
    private readonly INavigationDataBuilder _navigationDataBuilder;
    private readonly VisualMapDatasetBuilder _datasetBuilder;
    private readonly VisualMapStoragePaths _paths;
    private readonly ILogger<VisualMapService> _logger;
    private readonly ConcurrentDictionary<GameType, VisualMapDataset> _datasets = new();
    private readonly ConcurrentDictionary<GameType, SemaphoreSlim> _gates = new();

    public VisualMapService(
        IGameInstallationLocator installationLocator,
        INavigationDataBuilder navigationDataBuilder,
        VisualMapDatasetBuilder datasetBuilder,
        VisualMapStoragePaths paths,
        ILogger<VisualMapService> logger)
    {
        _installationLocator = installationLocator;
        _navigationDataBuilder = navigationDataBuilder;
        _datasetBuilder = datasetBuilder;
        _paths = paths;
        _logger = logger;
    }

    public event EventHandler<VisualMapBuildProgress>? BuildProgressChanged;

    public async Task<VisualMapStatus> BuildAsync(GameType game, CancellationToken cancellationToken = default)
    {
        var gate = _gates.GetOrAdd(game, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            var installation = FindInstallation(game);
            if (installation is null)
            {
                return Missing(game, "Game installation not found. Set a manual game path before building map data.");
            }

            var map = Data.NavigationStoragePaths.GetMapCode(game);
            var parserDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "TruckNav", "navigation", Data.NavigationStoragePaths.GetGameCode(game), "parser");
            if (!File.Exists(Path.Combine(parserDirectory, $"{map}-prefabDescriptions.json")))
            {
                Publish(new VisualMapBuildProgress(game, "Reading map sectors...", 0.01, "Generating installed-game parser data first"));
                var sourceStatus = await _navigationDataBuilder.BuildAsync(game, cancellationToken);
                if (!sourceStatus.IsReady)
                {
                    return Missing(game, sourceStatus.Message);
                }
            }

            var progress = new Progress<VisualMapBuildProgress>(Publish);
            var dataset = await Task.Run(
                () => _datasetBuilder.BuildAsync(installation, progress, cancellationToken),
                cancellationToken);
            var cachePath = _paths.GetDatasetPath(game);
            await Task.Run(() => CompactVisualMapCodec.Write(cachePath, dataset), cancellationToken);
            _datasets[game] = dataset;

            var tileRoot = _paths.GetTileCacheRoot(game);
            if (Directory.Exists(tileRoot))
            {
                Directory.Delete(tileRoot, recursive: true);
            }
            Directory.CreateDirectory(tileRoot);

            Publish(new VisualMapBuildProgress(game, "Ready", 1, "Detailed visual map is ready"));
            return await GetStatusAsync(game, cancellationToken);
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or JsonException or InvalidOperationException)
        {
            _logger.LogError(exception, "Visual map generation failed for {Game}.", game);
            Publish(new VisualMapBuildProgress(game, "Build failed", 1, exception.Message));
            return Missing(game, $"Visual map generation failed: {exception.Message}");
        }
        finally
        {
            gate.Release();
        }
    }

    public Task<VisualMapStatus> GetStatusAsync(GameType game, CancellationToken cancellationToken = default)
    {
        var path = _paths.GetDatasetPath(game);
        if (!File.Exists(path))
        {
            return Task.FromResult(Missing(game, "Visual map build required."));
        }

        try
        {
            var installation = FindInstallation(game);
            return Task.FromResult(CompactVisualMapCodec.ReadStatus(
                path,
                _paths.GetGameRoot(game),
                installation?.Version,
                installation?.DlcSignature));
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException)
        {
            return Task.FromResult(Missing(game, $"Visual map cache is invalid: {exception.Message}"));
        }
    }

    public async Task<VisualMapManifest?> GetManifestAsync(GameType game, CancellationToken cancellationToken = default)
    {
        var status = await GetStatusAsync(game, cancellationToken);
        if (!status.IsReady)
        {
            return null;
        }
        return (await GetDatasetAsync(game, cancellationToken))?.Manifest;
    }

    public async Task<VisualMapTile?> GetTileAsync(
        GameType game,
        int level,
        int tileX,
        int tileY,
        CancellationToken cancellationToken = default)
    {
        var levelDefinition = Levels.FirstOrDefault(item => item.Level == level);
        if (levelDefinition is null)
        {
            return null;
        }
        var dataset = await GetDatasetAsync(game, cancellationToken);
        if (dataset is null)
        {
            return null;
        }

        var tilePath = Path.Combine(_paths.GetTileCacheRoot(game), level.ToString(), tileX.ToString(), $"{tileY}.json.gz");
        var etag = CreateEtag(dataset.Manifest.GeneratedUtc, level, tileX, tileY);
        if (File.Exists(tilePath))
        {
            return new VisualMapTile(await File.ReadAllBytesAsync(tilePath, cancellationToken), "application/json", "gzip", etag);
        }

        var content = await Task.Run(
            () => CreateTile(dataset, levelDefinition, tileX, tileY),
            cancellationToken);
        Directory.CreateDirectory(Path.GetDirectoryName(tilePath)!);
        var temporaryPath = tilePath + ".tmp-" + Guid.NewGuid().ToString("N");
        await File.WriteAllBytesAsync(temporaryPath, content, cancellationToken);
        try
        {
            File.Move(temporaryPath, tilePath, overwrite: true);
        }
        catch (IOException) when (File.Exists(tilePath))
        {
            File.Delete(temporaryPath);
        }
        return new VisualMapTile(content, "application/json", "gzip", etag);
    }

    private async Task<VisualMapDataset?> GetDatasetAsync(GameType game, CancellationToken cancellationToken)
    {
        if (_datasets.TryGetValue(game, out var cached))
        {
            return cached;
        }
        var path = _paths.GetDatasetPath(game);
        if (!File.Exists(path))
        {
            return null;
        }
        try
        {
            var dataset = await Task.Run(() => CompactVisualMapCodec.Read(path), cancellationToken);
            _datasets[game] = dataset;
            return dataset;
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException)
        {
            _logger.LogWarning(exception, "Unable to load visual map cache for {Game}.", game);
            return null;
        }
    }

    private static byte[] CreateTile(VisualMapDataset dataset, VisualMapLevel level, int tileX, int tileY)
    {
        var minX = tileX * level.TileWorldSize;
        var minZ = tileY * level.TileWorldSize;
        var maxX = minX + level.TileWorldSize;
        var maxZ = minZ + level.TileWorldSize;
        using var buffer = new MemoryStream();
        using (var gzip = new GZipStream(buffer, CompressionLevel.Fastest, leaveOpen: true))
        using (var writer = new Utf8JsonWriter(gzip))
        {
            writer.WriteStartObject();
            writer.WriteNumber("version", 1);
            writer.WriteNumber("level", level.Level);
            writer.WriteNumber("x", tileX);
            writer.WriteNumber("y", tileY);

            writer.WriteStartArray("lines");
            foreach (var line in dataset.Lines)
            {
                if (!IncludeLine(line, level.Level) || !VisualMapGeometry.Intersects(line.Bounds, minX, minZ, maxX, maxZ))
                {
                    continue;
                }
                writer.WriteStartArray();
                writer.WriteNumberValue(line.Id);
                writer.WriteNumberValue((byte)line.Kind);
                writer.WriteNumberValue((byte)line.RoadClass);
                writer.WriteNumberValue(line.WidthMeters);
                WritePoints(writer, line.Points);
                writer.WriteEndArray();
            }
            writer.WriteEndArray();

            writer.WriteStartArray("polygons");
            if (level.Level > 0)
            {
                foreach (var polygon in dataset.Polygons)
                {
                    if (!VisualMapGeometry.Intersects(polygon.Bounds, minX, minZ, maxX, maxZ))
                    {
                        continue;
                    }
                    writer.WriteStartArray();
                    writer.WriteNumberValue(polygon.Id);
                    writer.WriteNumberValue(polygon.Color);
                    writer.WriteBooleanValue(polygon.DrawOver);
                    WritePoints(writer, polygon.Points);
                    writer.WriteEndArray();
                }
            }
            writer.WriteEndArray();

            writer.WriteStartArray("labels");
            foreach (var label in dataset.Labels)
            {
                if ((level.Level == 0 && label.Priority < 1) ||
                    label.Position.X < minX || label.Position.X > maxX ||
                    label.Position.Z < minZ || label.Position.Z > maxZ)
                {
                    continue;
                }
                writer.WriteStartArray();
                writer.WriteNumberValue(label.Id);
                writer.WriteStringValue(label.Text);
                writer.WriteNumberValue(label.Position.X);
                writer.WriteNumberValue(label.Position.Z);
                writer.WriteNumberValue(label.Priority);
                writer.WriteEndArray();
            }
            writer.WriteEndArray();

            writer.WriteStartArray("pois");
            if (level.Level > 0)
            {
                foreach (var point in dataset.PointsOfInterest)
                {
                    if ((level.Level == 1 && !IsNormalLevelPoi(point.Type)) ||
                        point.Position.X < minX || point.Position.X > maxX ||
                        point.Position.Z < minZ || point.Position.Z > maxZ)
                    {
                        continue;
                    }
                    writer.WriteStartArray();
                    writer.WriteNumberValue(point.Id);
                    writer.WriteStringValue(point.Type);
                    writer.WriteNumberValue(point.Position.X);
                    writer.WriteNumberValue(point.Position.Z);
                    if (point.Label is null) writer.WriteNullValue(); else writer.WriteStringValue(point.Label);
                    writer.WriteEndArray();
                }
            }
            writer.WriteEndArray();
            writer.WriteEndObject();
        }
        return buffer.ToArray();
    }

    private static bool IncludeLine(VisualLineFeature line, int level) => level switch
    {
        0 => line.Kind is VisualLineKind.Ferry or VisualLineKind.Train || line.RoadClass >= VisualRoadClass.Major,
        1 => line.Kind != VisualLineKind.Divider && line.RoadClass >= VisualRoadClass.Local,
        _ => true
    };

    private static bool IsNormalLevelPoi(string type) => type is
        "Fuel" or "Rest" or "Service" or "Ferry" or "Train" or "WeighStation";

    private static void WritePoints(Utf8JsonWriter writer, IReadOnlyList<WorldPoint> points)
    {
        writer.WriteStartArray();
        foreach (var point in points)
        {
            writer.WriteNumberValue(point.X);
            writer.WriteNumberValue(point.Z);
        }
        writer.WriteEndArray();
    }

    private GameInstallation? FindInstallation(GameType game) =>
        _installationLocator.FindInstallations().FirstOrDefault(item => item.Game == game);

    private VisualMapStatus Missing(GameType game, string message) => new(
        game,
        false,
        false,
        _paths.GetGameRoot(game),
        null,
        VisualMapDatasetBuilder.GeneratorVersion,
        0, 0, 0, 0, 0, 0, 0, null,
        message);

    private void Publish(VisualMapBuildProgress progress) =>
        BuildProgressChanged?.Invoke(this, progress);

    private static string CreateEtag(DateTimeOffset generatedUtc, int level, int x, int y)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{generatedUtc.UtcTicks}:{level}:{x}:{y}"));
        return $"\"{Convert.ToHexString(bytes.AsSpan(0, 8))}\"";
    }
}
