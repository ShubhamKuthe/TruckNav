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
    private readonly IGameCaptureService _captureService;
    private readonly IGpsCaptureProfileStore _profileStore;
    private ConnectorSessionSnapshot _lastSessionSnapshot;

    public DiagnosticsViewModel(
        IConnectorServer connectorServer,
        IConnectorSessionStore sessionStore,
        IGameCaptureService captureService,
        IGpsCaptureProfileStore profileStore,
        INavigationService navigationService)
        : base(PageId.Diagnostics, "Advanced / Diagnostics")
    {
        _connectorServer = connectorServer;
        _captureService = captureService;
        _profileStore = profileStore;
        _navigationService = navigationService;
        _lastSessionSnapshot = sessionStore.Snapshot;

        Refresh(sessionStore.Snapshot);
        sessionStore.SnapshotChanged += (_, snapshot) => Dispatch(() =>
        {
            _lastSessionSnapshot = snapshot;
            Refresh(snapshot);
        });
        captureService.SnapshotChanged += (_, _) => Dispatch(() => Refresh(_lastSessionSnapshot));
        profileStore.ProfilesChanged += (_, _) => Dispatch(() => Refresh(_lastSessionSnapshot));
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
            DeviceConnectionState.Connected => snapshot.GameDetection.RunningGames.Count == 0
                ? "Waiting for game"
                : "Waiting for GPS capture configuration",
            DeviceConnectionState.Reconnecting => "Waiting for reconnect",
            _ => "Waiting for device"
        };

        StreamDetails =
            $"State          {streamState}{Environment.NewLine}" +
            $"App Version    {snapshot.LocalConnector.AppVersion}{Environment.NewLine}" +
            $"Transport      WebSocket control{Environment.NewLine}" +
            $"Video          Not implemented in Phase 4";

        CaptureDetails = BuildCaptureDetails(snapshot.GameDetection, _captureService.CurrentSnapshot, _profileStore.GetAll());

        RecentActivity =
            $"Updated        {snapshot.LastUpdatedUtc:HH:mm:ss 'UTC'}{Environment.NewLine}" +
            $"Last Mobile    {snapshot.LastSeenMobileDevice?.DeviceName ?? "None"}{Environment.NewLine}" +
            $"Last Issue     {snapshot.LastIssueCode ?? "None"}{Environment.NewLine}" +
            $"Detail         {snapshot.LastIssueMessage ?? "No recent protocol issues"}";
    }

    private static string BuildCaptureDetails(
        GameDetectionSnapshot detection,
        CaptureRuntimeSnapshot capture,
        IReadOnlyDictionary<GameType, GpsCaptureProfile> profiles)
    {
        var lines = new List<string>
        {
            BuildGameDetectionDetails(detection),
            string.Empty,
            "CAPTURE",
            $"Running       {capture.IsRunning}",
            $"Source        {capture.Source?.DisplayName ?? "None"}",
            $"Resolution    {(capture.FrameWidth > 0 ? $"{capture.FrameWidth} x {capture.FrameHeight}" : "None")}",
            $"Preview FPS   {capture.PreviewFramesPerSecond:0.0}",
            $"Error         {capture.ErrorCode}",
            $"Detail        {capture.TechnicalDetail ?? "None"}",
            string.Empty,
            "SAVED PROFILES"
        };

        foreach (var definition in SupportedGameCatalog.All)
        {
            if (!profiles.TryGetValue(definition.GameType, out var profile))
            {
                lines.Add($"{definition.DisplayName}  Not configured");
                continue;
            }

            lines.Add($"{definition.DisplayName}  Configured");
            lines.Add($"Source        {profile.SourceType} / {profile.SourceId}");
            lines.Add($"Reference     {profile.ReferenceSourceWidth} x {profile.ReferenceSourceHeight}");
            lines.Add($"Crop          {profile.NormalizedX:0.0000}, {profile.NormalizedY:0.0000}, {profile.NormalizedWidth:0.0000}, {profile.NormalizedHeight:0.0000}");
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static string BuildGameDetectionDetails(GameDetectionSnapshot snapshot)
    {
        var lines = new List<string>
        {
            "GAME DETECTION"
        };

        foreach (var definition in SupportedGameCatalog.All)
        {
            var runningGame = snapshot.GetGame(definition.GameType);
            lines.Add(definition.DisplayName);
            lines.Add(runningGame is null ? "Not running" : "Running");

            if (runningGame is null)
            {
                lines.Add(string.Empty);
                continue;
            }

            lines.Add(runningGame.WindowAvailable ? "Window available" : "Window not available yet");
            lines.Add($"PID           {runningGame.ProcessId}");
            lines.Add($"Process       {runningGame.ProcessName}");
            lines.Add($"Handle        0x{runningGame.WindowHandle.ToInt64():X}");
            lines.Add($"Title         {runningGame.WindowTitle ?? "No window title yet"}");
            lines.Add(string.Empty);
        }

        return string.Join(Environment.NewLine, lines).TrimEnd();
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
