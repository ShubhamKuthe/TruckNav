using TruckNav.Connector.Core.Enums;
using TruckNav.Connector.Core.Models;

namespace TruckNav.Connector.Core.Interfaces;

public interface ITelemetryReader : IDisposable
{
    bool TryRead(out TelemetrySnapshot snapshot);
}

public interface IRoadGraphProvider
{
    Task<RoadGraph?> GetGraphAsync(GameType game, CancellationToken cancellationToken = default);

    void Invalidate(GameType game);
}

public interface IRoadGraphRepository
{
    Task<RoadGraph?> LoadAsync(GameType game, CancellationToken cancellationToken = default);

    Task<NavigationDataStatus> GetStatusAsync(GameType game, CancellationToken cancellationToken = default);
}

public interface ISpatialRoadIndex
{
    RoadNode? FindNearest(WorldPoint point, double maximumDistanceMeters = 500);
}

public interface IDestinationResolver
{
    ResolvedDestination? Resolve(TelemetrySnapshot telemetry, RoadGraph graph);
}

public interface IRoutePlanner
{
    RoutePlan? Plan(
        RoadGraph graph,
        WorldPoint start,
        double headingDegrees,
        ResolvedDestination destination,
        bool isReroute = false,
        CancellationToken cancellationToken = default);
}

public interface IRouteProgressTracker
{
    RouteProgressSnapshot Update(RoutePlan route, TelemetrySnapshot telemetry);

    void Reset();
}

public interface IGameCoordinateTransform
{
    WorldPoint ToMapPoint(TelemetrySnapshot telemetry);
}

public interface IGameInstallationLocator
{
    IReadOnlyList<GameInstallation> FindInstallations();

    bool TrySetManualPath(GameType game, string path, out string errorMessage);
}

public interface ITelemetryPluginInstaller
{
    TelemetryPluginStatus GetStatus(GameType game);

    Task<TelemetryPluginStatus> InstallAsync(GameType game, CancellationToken cancellationToken = default);
}

public interface INavigationDataBuilder
{
    Task<NavigationDataStatus> BuildAsync(GameType game, CancellationToken cancellationToken = default);

    Task<NavigationDataStatus> GetStatusAsync(GameType game, CancellationToken cancellationToken = default);
}

public interface IVisualMapService
{
    event EventHandler<VisualMapBuildProgress>? BuildProgressChanged;

    Task<VisualMapStatus> BuildAsync(GameType game, CancellationToken cancellationToken = default);

    Task<VisualMapStatus> GetStatusAsync(GameType game, CancellationToken cancellationToken = default);

    Task<VisualMapManifest?> GetManifestAsync(GameType game, CancellationToken cancellationToken = default);

    Task<VisualMapTile?> GetTileAsync(
        GameType game,
        int level,
        int tileX,
        int tileY,
        CancellationToken cancellationToken = default);
}
