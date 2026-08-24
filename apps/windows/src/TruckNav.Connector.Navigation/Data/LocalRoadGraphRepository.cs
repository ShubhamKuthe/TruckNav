using Microsoft.Extensions.Logging;
using TruckNav.Connector.Core.Enums;
using TruckNav.Connector.Core.Interfaces;
using TruckNav.Connector.Core.Models;

namespace TruckNav.Connector.Navigation.Data;

public sealed class LocalRoadGraphRepository : IRoadGraphRepository
{
    private readonly IGameInstallationLocator _installationLocator;
    private readonly NavigationStoragePaths _paths;
    private readonly TruckermudgeonGraphImporter _importer;
    private readonly ILogger<LocalRoadGraphRepository> _logger;

    public LocalRoadGraphRepository(
        IGameInstallationLocator installationLocator,
        NavigationStoragePaths paths,
        TruckermudgeonGraphImporter importer,
        ILogger<LocalRoadGraphRepository> logger)
    {
        _installationLocator = installationLocator;
        _paths = paths;
        _importer = importer;
        _logger = logger;
    }

    public async Task<RoadGraph?> LoadAsync(GameType game, CancellationToken cancellationToken = default)
    {
        var installation = _installationLocator.FindInstallations().FirstOrDefault(item => item.Game == game);
        if (installation is null)
        {
            return null;
        }

        var cachePath = _paths.GetCachePath(game);
        if (File.Exists(cachePath))
        {
            try
            {
                var cached = await Task.Run(() => CompactRoadGraphCodec.Read(cachePath), cancellationToken);
                if (string.Equals(cached.Metadata.GameVersion, installation.Version, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(cached.Metadata.DlcSignature, installation.DlcSignature, StringComparison.Ordinal))
                {
                    if (cached.PointsOfInterest.Count == 0)
                    {
                        var mapCode = NavigationStoragePaths.GetMapCode(game);
                        var poiPath = Path.Combine(_paths.GetParserDirectory(game), $"{mapCode}-pois.json");
                        var points = await TruckermudgeonGraphImporter.ReadPointsOfInterestAsync(
                            poiPath,
                            cancellationToken);
                        if (points.Count > 0)
                        {
                            cached = new RoadGraph(
                                cached.Metadata with { FormatVersion = 2, GeneratedUtc = DateTimeOffset.UtcNow },
                                cached.Nodes,
                                cached.Destinations,
                                points);
                            await Task.Run(() => CompactRoadGraphCodec.Write(cachePath, cached), cancellationToken);
                            _logger.LogInformation(
                                "Navigation cache for {Game} was enriched with {Count} verified map points.",
                                game,
                                points.Count);
                        }
                    }

                    return cached;
                }

                _logger.LogInformation("Navigation cache for {Game} is stale and will be rebuilt.", game);
            }
            catch (Exception exception) when (exception is IOException or InvalidDataException)
            {
                _logger.LogWarning(exception, "Navigation cache for {Game} is invalid and will be rebuilt.", game);
            }
        }

        if (!HasGeneratorOutput(game))
        {
            return null;
        }

        var graph = await _importer.ImportAsync(
            installation,
            _paths.GetParserDirectory(game),
            _paths.GetGeneratedDirectory(game),
            cancellationToken);
        await Task.Run(() => CompactRoadGraphCodec.Write(cachePath, graph), cancellationToken);
        return graph;
    }

    public Task<NavigationDataStatus> GetStatusAsync(GameType game, CancellationToken cancellationToken = default)
    {
        var root = _paths.GetGameRoot(game);
        var cache = _paths.GetCachePath(game);
        if (File.Exists(cache))
        {
            try
            {
                return Task.FromResult(CompactRoadGraphCodec.ReadStatus(cache, root));
            }
            catch (Exception exception) when (exception is IOException or InvalidDataException)
            {
                return Task.FromResult(new NavigationDataStatus(
                    game,
                    false,
                    root,
                    null,
                    0,
                    0,
                    null,
                    $"Navigation cache is invalid: {exception.Message}"));
            }
        }

        var sourceReady = HasGeneratorOutput(game);
        return Task.FromResult(new NavigationDataStatus(
            game,
            sourceReady,
            root,
            null,
            0,
            0,
            sourceReady ? DateTimeOffset.UtcNow : null,
            sourceReady
                ? "Generated source data is ready for first native import."
                : "Navigation data has not been generated for this game."));
    }

    private bool HasGeneratorOutput(GameType game)
    {
        var map = NavigationStoragePaths.GetMapCode(game);
        var parser = _paths.GetParserDirectory(game);
        return File.Exists(Path.Combine(_paths.GetGeneratedDirectory(game), $"{map}-graph.json")) &&
               File.Exists(Path.Combine(parser, $"{map}-nodes.json")) &&
               File.Exists(Path.Combine(parser, $"{map}-companies.json")) &&
               File.Exists(Path.Combine(parser, $"{map}-cities.json")) &&
               File.Exists(Path.Combine(parser, $"{map}-companyDefs.json"));
    }
}
