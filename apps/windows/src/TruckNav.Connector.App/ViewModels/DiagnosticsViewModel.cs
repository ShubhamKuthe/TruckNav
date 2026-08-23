using System.Net;
using System.Net.NetworkInformation;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TruckNav.Connector.App.Services;
using TruckNav.Connector.Core.Enums;
using TruckNav.Connector.Networking.Hosting;
using TruckNav.Connector.Networking.Session;

namespace TruckNav.Connector.App.ViewModels;

public sealed partial class DiagnosticsViewModel : PageViewModelBase
{
    private readonly IConnectorServer _connectorServer;
    private readonly INavigationService _navigationService;

    public DiagnosticsViewModel(
        IConnectorServer connectorServer,
        IConnectorSessionStore sessionStore,
        INavigationService navigationService)
        : base(PageId.Diagnostics, "Advanced / Diagnostics")
    {
        _connectorServer = connectorServer;
        _navigationService = navigationService;

        Refresh(sessionStore.Snapshot);
        sessionStore.SnapshotChanged += (_, snapshot) => Dispatch(() => Refresh(snapshot));
    }

    [ObservableProperty]
    private string connectionDetails = string.Empty;

    [ObservableProperty]
    private string streamDetails = string.Empty;

    [ObservableProperty]
    private string captureDetails = string.Empty;

    [ObservableProperty]
    private string recentActivity = string.Empty;

    [RelayCommand]
    private void GoBack() => _navigationService.NavigateTo(PageId.Settings);

    private void Refresh(ConnectorSessionSnapshot snapshot)
    {
        var lanAddresses = GetReachableLanAddresses();
        var localLan = lanAddresses.Count == 0
            ? "No LAN address"
            : string.Join(", ", lanAddresses);
        var listenerPort = _connectorServer.ServiceUri?.Port.ToString() ?? "Starting";
        var activePeer = snapshot.ActiveMobileDevice?.DeviceName ?? "None";
        var activePeerPlatform = snapshot.ActiveMobileDevice?.Platform ?? "—";

        ConnectionDetails =
            $"Local LAN      {localLan}{Environment.NewLine}" +
            $"Listener Port  {listenerPort}{Environment.NewLine}" +
            $"Peer           {activePeer}{Environment.NewLine}" +
            $"Peer Platform  {activePeerPlatform}{Environment.NewLine}" +
            $"Protocol       v{snapshot.LocalConnector.ProtocolVersion}";

        var streamState = snapshot.DeviceState switch
        {
            DeviceConnectionState.Connected => "Waiting for game",
            DeviceConnectionState.Reconnecting => "Waiting for reconnect",
            _ => "Waiting for device"
        };

        StreamDetails =
            $"State          {streamState}{Environment.NewLine}" +
            $"App Version    {snapshot.LocalConnector.AppVersion}{Environment.NewLine}" +
            $"Transport      WebSocket control{Environment.NewLine}" +
            $"Metrics        Available when streaming starts";

        CaptureDetails =
            $"Game           Not detected{Environment.NewLine}" +
            $"Profile        Not configured{Environment.NewLine}" +
            $"Source         Auto{Environment.NewLine}" +
            $"Preview        Requires a live game window";

        RecentActivity =
            $"Updated        {snapshot.LastUpdatedUtc:HH:mm:ss 'UTC'}{Environment.NewLine}" +
            $"Last Mobile    {snapshot.LastSeenMobileDevice?.DeviceName ?? "None"}{Environment.NewLine}" +
            $"Last Issue     {snapshot.LastIssueCode ?? "None"}{Environment.NewLine}" +
            $"Detail         {snapshot.LastIssueMessage ?? "No recent protocol issues"}";
    }

    private static IReadOnlyList<string> GetReachableLanAddresses()
    {
        return NetworkInterface.GetAllNetworkInterfaces()
            .Where(networkInterface =>
                networkInterface.OperationalStatus == OperationalStatus.Up &&
                networkInterface.NetworkInterfaceType is not NetworkInterfaceType.Loopback and
                    not NetworkInterfaceType.Tunnel)
            .SelectMany(networkInterface => networkInterface.GetIPProperties().UnicastAddresses)
            .Select(unicastAddress => unicastAddress.Address)
            .Where(address =>
                !IPAddress.IsLoopback(address) &&
                !address.IsIPv6LinkLocal &&
                !(address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork &&
                  address.GetAddressBytes() is [169, 254, ..]))
            .Select(address => address.ToString())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(address => address, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static void Dispatch(Action action)
    {
        if (Application.Current.Dispatcher.CheckAccess())
        {
            action();
            return;
        }

        Application.Current.Dispatcher.Invoke(action);
    }
}
