using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TruckNav.Connector.Core.Enums;
using TruckNav.Connector.Core.Interfaces;
using TruckNav.Connector.Core.Models;
using TruckNav.Connector.Core.Utilities;

namespace TruckNav.Connector.App.ViewModels;

public sealed partial class NavigationViewModel : PageViewModelBase
{
    private readonly INavigationDataBuilder _navigationDataBuilder;
    private readonly IVisualMapService _visualMapService;

    public NavigationViewModel(
        INavigationStateStore navigationStateStore,
        INavigationDataBuilder navigationDataBuilder,
        IVisualMapService visualMapService)
        : base(PageId.Navigation, "Navigation")
    {
        _navigationDataBuilder = navigationDataBuilder;
        _visualMapService = visualMapService;
        ApplySnapshot(navigationStateStore.Snapshot);
        navigationStateStore.SnapshotChanged += (_, snapshot) => Dispatch(() => ApplySnapshot(snapshot));
        visualMapService.BuildProgressChanged += (_, progress) => Dispatch(() => ApplyBuildProgress(progress));
        _ = RefreshMapDataAsync();
    }

    [ObservableProperty]
    private string status = "Waiting for telemetry";

    [ObservableProperty]
    private string statusMessage = "Start ETS2 or ATS to begin.";

    [ObservableProperty]
    private string game = "No game connected";

    [ObservableProperty]
    private string telemetry = "Shared memory is not connected.";

    [ObservableProperty]
    private string destination = "No active delivery";

    [ObservableProperty]
    private string route = "No route calculated";

    [ObservableProperty]
    private string nextManeuver = "Waiting for an active job";

    [ObservableProperty]
    private string progress = "Remaining distance and time will appear here.";

    [ObservableProperty]
    private string routeMetadata = "Route revision 0";

    [ObservableProperty]
    private StatusTone statusTone = StatusTone.Neutral;

    [ObservableProperty]
    private bool isOffRoute;

    [ObservableProperty]
    private string ets2NavigationGraph = "Checking...";

    [ObservableProperty]
    private string ets2VisualMap = "Checking...";

    [ObservableProperty]
    private string ets2MapAction = "Build Map";

    [ObservableProperty]
    private string atsNavigationGraph = "Checking...";

    [ObservableProperty]
    private string atsVisualMap = "Checking...";

    [ObservableProperty]
    private string atsMapAction = "Build Map";

    [ObservableProperty]
    private bool isVisualMapBuildRunning;

    [ObservableProperty]
    private string visualMapBuildStage = "Map generation runs locally and is needed only after game data changes.";

    [ObservableProperty]
    private double visualMapBuildPercent;

    partial void OnIsVisualMapBuildRunningChanged(bool value)
    {
        BuildEts2MapCommand.NotifyCanExecuteChanged();
        BuildAtsMapCommand.NotifyCanExecuteChanged();
        RefreshMapDataCommand.NotifyCanExecuteChanged();
    }

    private bool CanManageMapData() => !IsVisualMapBuildRunning;

    [RelayCommand(CanExecute = nameof(CanManageMapData))]
    private async Task BuildEts2MapAsync() =>
        await BuildVisualMapAsync(GameType.EuroTruckSimulator2);

    [RelayCommand(CanExecute = nameof(CanManageMapData))]
    private async Task BuildAtsMapAsync() =>
        await BuildVisualMapAsync(GameType.AmericanTruckSimulator);

    [RelayCommand(CanExecute = nameof(CanManageMapData))]
    private async Task RefreshMapDataAsync()
    {
        var ets2Graph = await _navigationDataBuilder.GetStatusAsync(GameType.EuroTruckSimulator2);
        var atsGraph = await _navigationDataBuilder.GetStatusAsync(GameType.AmericanTruckSimulator);
        var ets2Map = await _visualMapService.GetStatusAsync(GameType.EuroTruckSimulator2);
        var atsMap = await _visualMapService.GetStatusAsync(GameType.AmericanTruckSimulator);
        Dispatch(() =>
        {
            Ets2NavigationGraph = FormatGraphStatus(ets2Graph);
            AtsNavigationGraph = FormatGraphStatus(atsGraph);
            Ets2VisualMap = FormatVisualMapStatus(ets2Map);
            AtsVisualMap = FormatVisualMapStatus(atsMap);
            Ets2MapAction = ets2Map.IsReady ? "Rebuild Map Data" : "Build Map";
            AtsMapAction = atsMap.IsReady ? "Rebuild Map Data" : "Build Map";
        });
    }

    private async Task BuildVisualMapAsync(GameType gameType)
    {
        IsVisualMapBuildRunning = true;
        VisualMapBuildPercent = 0;
        VisualMapBuildStage = $"Preparing {SupportedGameCatalog.GetDisplayName(gameType)} map data...";
        try
        {
            var status = await _visualMapService.BuildAsync(gameType);
            VisualMapBuildStage = status.Message;
            await RefreshMapDataAsync();
        }
        finally
        {
            IsVisualMapBuildRunning = false;
        }
    }

    private void ApplyBuildProgress(VisualMapBuildProgress progressValue)
    {
        VisualMapBuildStage = $"{progressValue.Stage}  {progressValue.Message}";
        VisualMapBuildPercent = Math.Clamp(progressValue.Fraction * 100, 0, 100);
    }

    private static string FormatGraphStatus(NavigationDataStatus statusValue) =>
        statusValue.IsReady
            ? $"Ready  |  {statusValue.NodeCount:N0} nodes, {statusValue.EdgeCount:N0} edges"
            : statusValue.Message;

    private static string FormatVisualMapStatus(VisualMapStatus statusValue)
    {
        if (!statusValue.IsReady)
        {
            return statusValue.Message;
        }
        return $"Ready  |  {statusValue.RoadCount:N0} roads, {statusValue.PrefabRoadCount:N0} prefab paths, " +
               $"{statusValue.CityCount:N0} cities, {statusValue.PointOfInterestCount:N0} POIs  |  " +
               $"{statusValue.CacheBytes / (1024d * 1024):0.0} MB";
    }

    private void ApplySnapshot(NavigationSnapshot snapshot)
    {
        var value = snapshot.Telemetry;
        Status = SplitWords(snapshot.Status.ToString());
        StatusMessage = snapshot.StatusMessage;
        StatusTone = snapshot.Status switch
        {
            NavigationRuntimeStatus.Navigating => StatusTone.Success,
            NavigationRuntimeStatus.Rerouting or NavigationRuntimeStatus.CalculatingRoute => StatusTone.Accent,
            NavigationRuntimeStatus.Faulted or NavigationRuntimeStatus.RouteUnavailable => StatusTone.Error,
            NavigationRuntimeStatus.NavigationDataMissing or NavigationRuntimeStatus.DestinationUnresolved => StatusTone.Warning,
            _ => StatusTone.Neutral
        };

        Game = value.Connected
            ? $"{SupportedGameCatalog.GetDisplayName(value.Game)} {value.GameVersion}".Trim()
            : "No game connected";
        Telemetry = value.Connected
            ? $"{value.SpeedKph:0} km/h  |  limit {value.SpeedLimitKph:0} km/h  |  heading {value.HeadingDegrees:0.0} deg{Environment.NewLine}" +
              $"X {value.Position.X:0.0}  Z {value.Position.Z:0.0}  elevation {value.Elevation:0.0}"
            : "Shared memory is not connected.";
        Destination = value.OnJob
            ? $"{value.DestinationCompanyName ?? value.DestinationCompanyId ?? "Unknown company"}, " +
              $"{value.DestinationCityName ?? value.DestinationCityId ?? "unknown city"}"
            : "No active delivery";

        if (snapshot.Route is { } route)
        {
            Route = $"{route.Points.Count:N0} points  |  {FormatDistance(route.TotalDistanceMeters)}  |  " +
                    $"{FormatDuration(route.EstimatedTimeSeconds)}";
            RouteMetadata = $"Route revision {snapshot.RouteRevision}  |  " +
                            $"{(route.IsReroute ? "reroute" : "initial route")}  |  {route.CalculatedUtc.LocalDateTime:T}";
        }
        else
        {
            Route = "No route calculated";
            RouteMetadata = $"Route revision {snapshot.RouteRevision}";
        }

        if (snapshot.Progress is { } current)
        {
            NextManeuver = $"{SplitWords(current.NextManeuver.ToString())}: {current.NextInstruction}";
            Progress = $"{FormatDistance(current.DistanceToNextManeuverMeters)} to maneuver  |  " +
                       $"{FormatDistance(current.RemainingDistanceMeters)} remaining  |  " +
                       $"{FormatDuration(current.RemainingTimeSeconds)}";
            IsOffRoute = current.IsOffRoute;
        }
        else
        {
            NextManeuver = "Waiting for an active route";
            Progress = "Remaining distance and time will appear here.";
            IsOffRoute = false;
        }
    }

    private static string FormatDistance(double meters) => meters >= 1_000
        ? $"{meters / 1_000:0.0} km"
        : $"{Math.Max(0, meters):0} m";

    private static string FormatDuration(double seconds)
    {
        var duration = TimeSpan.FromSeconds(Math.Max(0, seconds));
        return duration.TotalHours >= 1 ? $"{(int)duration.TotalHours}h {duration.Minutes}m" : $"{duration.Minutes} min";
    }

    private static string SplitWords(string value) =>
        System.Text.RegularExpressions.Regex.Replace(value, "(?<!^)([A-Z])", " $1");

    private static void Dispatch(Action action)
    {
        if (Application.Current.Dispatcher.CheckAccess())
        {
            action();
        }
        else
        {
            Application.Current.Dispatcher.Invoke(action);
        }
    }
}
