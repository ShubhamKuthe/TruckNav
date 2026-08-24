using System.Net;
using System.Net.NetworkInformation;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TruckNav.Connector.App.Services;
using TruckNav.Connector.Core.Enums;
using TruckNav.Connector.Core.Interfaces;
using TruckNav.Connector.Core.Models;
using TruckNav.Connector.Core.Utilities;
using TruckNav.Connector.Networking.Hosting;
using TruckNav.Connector.Networking.Session;

namespace TruckNav.Connector.App.ViewModels;

public sealed partial class DiagnosticsViewModel : PageViewModelBase
{
    private readonly IConnectorServer _connectorServer;
    private readonly INavigationService _navigationService;
    private readonly IGameInstallationLocator _installationLocator;
    private readonly ITelemetryPluginInstaller _pluginInstaller;
    private readonly INavigationDataBuilder _dataBuilder;
    private ConnectorSessionSnapshot _sessionSnapshot;
    private NavigationSnapshot _navigationSnapshot;

    public DiagnosticsViewModel(
        IConnectorServer connectorServer,
        IConnectorSessionStore sessionStore,
        INavigationStateStore navigationStateStore,
        IGameInstallationLocator installationLocator,
        ITelemetryPluginInstaller pluginInstaller,
        INavigationDataBuilder dataBuilder,
        INavigationService navigationService)
        : base(PageId.Diagnostics, "Advanced / Diagnostics")
    {
        _connectorServer = connectorServer;
        _navigationService = navigationService;
        _installationLocator = installationLocator;
        _pluginInstaller = pluginInstaller;
        _dataBuilder = dataBuilder;
        _sessionSnapshot = sessionStore.Snapshot;
        _navigationSnapshot = navigationStateStore.Snapshot;

        Refresh();
        _ = RefreshSetupAsync();
        sessionStore.SnapshotChanged += (_, snapshot) => Dispatch(() =>
        {
            _sessionSnapshot = snapshot;
            Refresh();
        });
        navigationStateStore.SnapshotChanged += (_, snapshot) => Dispatch(() =>
        {
            _navigationSnapshot = snapshot;
            Refresh();
        });
    }

    [ObservableProperty]
    private string connectionDetails = string.Empty;

    [ObservableProperty]
    private string navigationDetails = string.Empty;

    [ObservableProperty]
    private string setupDetails = "Reading installed game setup...";

    [ObservableProperty]
    private string recentActivity = string.Empty;

    [RelayCommand]
    private void GoBack() => _navigationService.NavigateTo(PageId.Settings);

    [RelayCommand]
    private Task RefreshSetup() => RefreshSetupAsync();

    private void Refresh()
    {
        var lanAddresses = GetReachableLanAddresses();
        var localLan = lanAddresses.Count == 0 ? "No LAN address" : string.Join(", ", lanAddresses);
        var listenerPort = _connectorServer.ServiceUri?.Port.ToString() ?? "Starting";

        ConnectionDetails =
            $"Local LAN      {localLan}{Environment.NewLine}" +
            $"Listener Port  {listenerPort}{Environment.NewLine}" +
            $"Peer           {_sessionSnapshot.ActiveMobileDevice?.DeviceName ?? "None"}{Environment.NewLine}" +
            $"Protocol       v{_sessionSnapshot.LocalConnector.ProtocolVersion}{Environment.NewLine}" +
            $"Capabilities   {string.Join(", ", _sessionSnapshot.LocalConnector.Capabilities)}";

        var telemetry = _navigationSnapshot.Telemetry;
        var route = _navigationSnapshot.Route;
        var progress = _navigationSnapshot.Progress;
        NavigationDetails =
            $"Status         {_navigationSnapshot.Status}{Environment.NewLine}" +
            $"Detail         {_navigationSnapshot.StatusMessage}{Environment.NewLine}" +
            $"MMF Connected  {telemetry.Connected}{Environment.NewLine}" +
            $"Game           {telemetry.Game} {telemetry.GameVersion}{Environment.NewLine}" +
            $"Plugin Rev     {telemetry.PluginRevision}{Environment.NewLine}" +
            $"Timestamp      {telemetry.GameTimestamp}{Environment.NewLine}" +
            $"Position       {telemetry.Position.X:0.00}, {telemetry.Position.Z:0.00}{Environment.NewLine}" +
            $"Destination    {telemetry.DestinationKey}{Environment.NewLine}" +
            $"Route          {route?.RouteId ?? "None"}{Environment.NewLine}" +
            $"Route Revision {_navigationSnapshot.RouteRevision}{Environment.NewLine}" +
            $"Route Points   {route?.Points.Count ?? 0:N0}{Environment.NewLine}" +
            $"Segment        {progress?.SegmentIndex.ToString() ?? "None"}{Environment.NewLine}" +
            $"Off Route      {progress?.IsOffRoute.ToString() ?? "False"}";

        RecentActivity =
            $"Navigation     {_navigationSnapshot.LastUpdatedUtc:HH:mm:ss 'UTC'}{Environment.NewLine}" +
            $"Session        {_sessionSnapshot.LastUpdatedUtc:HH:mm:ss 'UTC'}{Environment.NewLine}" +
            $"Last Mobile    {_sessionSnapshot.LastSeenMobileDevice?.DeviceName ?? "None"}{Environment.NewLine}" +
            $"Last Issue     {_sessionSnapshot.LastIssueCode ?? "None"}{Environment.NewLine}" +
            $"Detail         {_sessionSnapshot.LastIssueMessage ?? "No recent protocol issues"}";
    }

    private async Task RefreshSetupAsync()
    {
        var installations = await Task.Run(() => _installationLocator.FindInstallations());
        var lines = new List<string>();
        foreach (var definition in SupportedGameCatalog.All)
        {
            var installation = installations.FirstOrDefault(value => value.Game == definition.GameType);
            var plugin = await Task.Run(() => _pluginInstaller.GetStatus(definition.GameType));
            var data = await _dataBuilder.GetStatusAsync(definition.GameType);
            lines.Add(definition.DisplayName.ToUpperInvariant());
            lines.Add($"Installed      {installation is not null}");
            lines.Add($"Version        {installation?.Version ?? "None"}");
            lines.Add($"Path           {installation?.InstallPath ?? "None"}");
            lines.Add($"Plugin         {plugin.Message}");
            lines.Add($"Graph          {data.Message}");
            lines.Add($"Nodes / Edges  {data.NodeCount:N0} / {data.EdgeCount:N0}");
            lines.Add(string.Empty);
        }

        Dispatch(() => SetupDetails = string.Join(Environment.NewLine, lines).TrimEnd());
    }

    private static IReadOnlyList<string> GetReachableLanAddresses() =>
        NetworkInterface.GetAllNetworkInterfaces()
            .Where(value => value.OperationalStatus == OperationalStatus.Up &&
                            value.NetworkInterfaceType is not NetworkInterfaceType.Loopback and not NetworkInterfaceType.Tunnel)
            .SelectMany(value => value.GetIPProperties().UnicastAddresses)
            .Select(value => value.Address)
            .Where(value => !IPAddress.IsLoopback(value) && !value.IsIPv6LinkLocal)
            .Select(value => value.ToString())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray();

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
