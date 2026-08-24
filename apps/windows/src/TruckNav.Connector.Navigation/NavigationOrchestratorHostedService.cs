using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TruckNav.Connector.Core.Enums;
using TruckNav.Connector.Core.Interfaces;
using TruckNav.Connector.Core.Models;

namespace TruckNav.Connector.Navigation;

public sealed class NavigationOrchestratorHostedService : BackgroundService
{
    private static readonly TimeSpan UpdateInterval = TimeSpan.FromMilliseconds(50);

    private readonly INavigationStateStore _stateStore;
    private readonly IRoadGraphProvider _graphProvider;
    private readonly IDestinationResolver _destinationResolver;
    private readonly IRoutePlanner _routePlanner;
    private readonly IRouteProgressTracker _progressTracker;
    private readonly IGameCoordinateTransform _coordinateTransform;
    private readonly ILogger<NavigationOrchestratorHostedService> _logger;

    private RoadGraph? _graph;
    private GameType _graphGame = GameType.None;
    private string? _navigationTargetKey;
    private ulong _lastTelemetryTimestamp;

    public NavigationOrchestratorHostedService(
        INavigationStateStore stateStore,
        IRoadGraphProvider graphProvider,
        IDestinationResolver destinationResolver,
        IRoutePlanner routePlanner,
        IRouteProgressTracker progressTracker,
        IGameCoordinateTransform coordinateTransform,
        ILogger<NavigationOrchestratorHostedService> logger)
    {
        _stateStore = stateStore;
        _graphProvider = graphProvider;
        _destinationResolver = destinationResolver;
        _routePlanner = routePlanner;
        _progressTracker = progressTracker;
        _coordinateTransform = coordinateTransform;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(UpdateInterval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await ProcessSnapshotAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Native navigation pipeline failed.");
                _stateStore.UpdateStatus(NavigationRuntimeStatus.Faulted, exception.Message);
            }
        }
    }

    private async Task ProcessSnapshotAsync(CancellationToken cancellationToken)
    {
        var snapshot = _stateStore.Snapshot;
        var telemetry = snapshot.Telemetry;
        if (!telemetry.Connected || telemetry.Game == GameType.None)
        {
            ResetRoute(snapshot, NavigationRuntimeStatus.WaitingForTelemetry, "Start ETS2 or ATS to begin.");
            return;
        }

        if (_graphGame != telemetry.Game)
        {
            _graph = null;
            _graphGame = telemetry.Game;
            _navigationTargetKey = null;
            _lastTelemetryTimestamp = 0;
            _progressTracker.Reset();
        }

        if (!telemetry.HasNavigationTarget)
        {
            ResetRoute(snapshot, NavigationRuntimeStatus.NoActiveJob, "Choose a Freight Market job or accept a delivery to create a route.");
            return;
        }

        if (_graph is null)
        {
            _stateStore.UpdateStatus(NavigationRuntimeStatus.CalculatingRoute, "Loading installed game navigation data...");
            _graph = await _graphProvider.GetGraphAsync(telemetry.Game, cancellationToken);
            if (_graph is null)
            {
                ResetRoute(
                    _stateStore.Snapshot,
                    NavigationRuntimeStatus.NavigationDataMissing,
                    "Build local navigation data for this game in the Games page.");
                return;
            }

            telemetry = _stateStore.Snapshot.Telemetry;
            if (!telemetry.Connected || telemetry.Game != _graphGame || !telemetry.HasNavigationTarget)
            {
                return;
            }
        }

        var targetChanged = !string.Equals(
            _navigationTargetKey,
            telemetry.NavigationTargetKey,
            StringComparison.Ordinal);
        var navigationSnapshot = _stateStore.Snapshot;
        var route = navigationSnapshot.Route;
        var priorResolutionFailed = navigationSnapshot.Status is
            NavigationRuntimeStatus.DestinationUnresolved or
            NavigationRuntimeStatus.RouteUnavailable;
        if (targetChanged || (route is null && !priorResolutionFailed))
        {
            _navigationTargetKey = telemetry.NavigationTargetKey;
            _progressTracker.Reset();
            var targetKind = telemetry.IsNavigatingToPickup ? "pickup location" : "delivery destination";
            _stateStore.UpdateStatus(NavigationRuntimeStatus.ResolvingDestination, $"Resolving the {targetKind}...");
            var destination = _destinationResolver.Resolve(telemetry, _graph);
            if (destination is null)
            {
                ResetRoute(
                    _stateStore.Snapshot,
                    NavigationRuntimeStatus.DestinationUnresolved,
                    $"The {targetKind} is not present in the installed map data.",
                    keepNavigationTargetKey: true);
                return;
            }

            _stateStore.UpdateStatus(NavigationRuntimeStatus.CalculatingRoute, $"Calculating route to {destination.DisplayName}...");
            route = await PlanAsync(telemetry, destination, isReroute: false, cancellationToken);
            if (route is null)
            {
                ResetRoute(
                    _stateStore.Snapshot,
                    NavigationRuntimeStatus.RouteUnavailable,
                    "No drivable route was found to the job destination.",
                    keepNavigationTargetKey: true);
                return;
            }

            _stateStore.UpdateRoute(
                route,
                NavigationRuntimeStatus.Navigating,
                $"Route ready to {route.Destination.DisplayName}.");
            _lastTelemetryTimestamp = 0;
        }

        if (route is null)
        {
            return;
        }

        if (telemetry.GameTimestamp == _lastTelemetryTimestamp)
        {
            return;
        }

        _lastTelemetryTimestamp = telemetry.GameTimestamp;
        var progress = _progressTracker.Update(route, telemetry);
        if (progress.ShouldReroute)
        {
            _stateStore.UpdateStatus(NavigationRuntimeStatus.Rerouting, "Recalculating from the current road...");
            var reroute = await PlanAsync(telemetry, route.Destination, isReroute: true, cancellationToken);
            if (reroute is not null)
            {
                route = reroute;
                _progressTracker.Reset();
                _stateStore.UpdateRoute(
                    route,
                    NavigationRuntimeStatus.Navigating,
                    $"Route updated to {route.Destination.DisplayName}.");
                progress = _progressTracker.Update(route, telemetry);
            }
            else
            {
                _logger.LogWarning("A reroute to {Destination} could not be calculated.", route.Destination.DisplayName);
            }
        }

        _stateStore.UpdateProgress(
            progress,
            NavigationRuntimeStatus.Navigating,
            progress.IsOffRoute ? "Return to the highlighted route." : progress.NextInstruction);
    }

    private Task<RoutePlan?> PlanAsync(
        TelemetrySnapshot telemetry,
        ResolvedDestination destination,
        bool isReroute,
        CancellationToken cancellationToken)
    {
        var start = _coordinateTransform.ToMapPoint(telemetry);
        return Task.Run(
            () => _routePlanner.Plan(
                _graph!,
                start,
                telemetry.HeadingDegrees,
                destination,
                isReroute,
                cancellationToken),
            cancellationToken);
    }

    private void ResetRoute(
        NavigationSnapshot snapshot,
        NavigationRuntimeStatus status,
        string message,
        bool keepNavigationTargetKey = false)
    {
        if (snapshot.Route is not null || snapshot.Progress is not null)
        {
            _stateStore.UpdateRoute(null, status, message);
        }
        else
        {
            _stateStore.UpdateStatus(status, message);
        }

        if (!keepNavigationTargetKey)
        {
            _navigationTargetKey = null;
        }

        _lastTelemetryTimestamp = 0;
        _progressTracker.Reset();
    }
}
