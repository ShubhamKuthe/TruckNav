using System.Collections.ObjectModel;
using TruckNav.Connector.Core.Enums;
using TruckNav.Connector.Networking.Session;

namespace TruckNav.Connector.App.ViewModels;

public sealed class HomeViewModel : PageViewModelBase
{
    private readonly IConnectorSessionStore _sessionStore;

    public HomeViewModel(IConnectorSessionStore sessionStore)
        : base(
            PageId.Home,
            "Home",
            "The Connector home page exposes only the four product-level states that matter: mobile device, game, GPS capture, and stream.")
    {
        _sessionStore = sessionStore;
        Cards = new ObservableCollection<StatusCardViewModel>();

        RefreshCards(_sessionStore.Snapshot);
        _sessionStore.SnapshotChanged += (_, snapshot) => Dispatch(() => RefreshCards(snapshot));
    }

    public ObservableCollection<StatusCardViewModel> Cards { get; }

    private void RefreshCards(ConnectorSessionSnapshot snapshot)
    {
        Cards.Clear();

        foreach (var card in CreateCards(snapshot))
        {
            Cards.Add(card);
        }
    }

    private static IReadOnlyList<StatusCardViewModel> CreateCards(ConnectorSessionSnapshot snapshot)
    {
        var mobileName = snapshot.ActiveMobileDevice?.DeviceName ?? snapshot.LastSeenMobileDevice?.DeviceName ?? "No mobile device";

        var mobileCard = snapshot.DeviceState switch
        {
            DeviceConnectionState.Connected => new StatusCardViewModel
            {
                Title = "Mobile Device",
                State = "Connected",
                Detail = $"{mobileName} is connected over the TruckNav local control channel.",
                Hint = "V1 supports one active mobile session at a time while discovery still identifies peers by persistent device ID.",
                Tone = StatusTone.Success
            },
            DeviceConnectionState.Reconnecting => new StatusCardViewModel
            {
                Title = "Mobile Device",
                State = "Reconnecting",
                Detail = $"{mobileName} disconnected unexpectedly and is expected to retry discovery plus handshake.",
                Hint = "Android reconnect backoff is approximately 1s, 2s, 3s, then 5s intervals with small jitter.",
                Tone = StatusTone.Accent
            },
            _ => new StatusCardViewModel
            {
                Title = "Mobile Device",
                State = "Waiting for mobile",
                Detail = snapshot.LastSeenMobileDevice is null
                    ? "No Android device has completed a hello handshake yet."
                    : $"{mobileName} was the most recent known client and can reconnect after LAN rediscovery.",
                Hint = "TruckNav never uses IP address, MAC address, or hostname as the permanent device identity.",
                Tone = StatusTone.Warning
            }
        };

        var streamState = snapshot.DeviceState switch
        {
            DeviceConnectionState.Connected => "Waiting for game",
            DeviceConnectionState.Reconnecting => "Reconnecting control session",
            _ => "Waiting for device"
        };

        var streamDetail = snapshot.DeviceState switch
        {
            DeviceConnectionState.Connected =>
                "Phase 2 stops at control signaling, so the stream remains idle until game detection and capture ship in later phases.",
            DeviceConnectionState.Reconnecting =>
                "The control WebSocket dropped unexpectedly, so streaming stays blocked until the mobile session successfully reconnects.",
            _ =>
                "The stream lane remains idle until a mobile device is connected, a supported game is detected, and GPS capture has been configured."
        };

        return
        [
            mobileCard,
            new StatusCardViewModel
            {
                Title = "Game",
                State = "Waiting for ETS2 or ATS",
                Detail = "Phase 2 keeps game detection honest: no supported title is mocked as running in production state.",
                Hint = "The connector can stay healthy and discoverable even while the PC is idle.",
                Tone = StatusTone.Accent
            },
            new StatusCardViewModel
            {
                Title = "GPS Capture",
                State = "Not Configured",
                Detail = "Capture setup remains a later milestone and still needs per-game Route Advisor crop profiles.",
                Hint = "No fake navigation map is rendered here; the future GPS surface will be fed by the Windows capture pipeline.",
                Tone = StatusTone.Warning
            },
            new StatusCardViewModel
            {
                Title = "Stream",
                State = streamState,
                Detail = streamDetail,
                Hint = "WebRTC, video encoding, and live game capture remain out of scope until later phases.",
                Tone = snapshot.DeviceState == DeviceConnectionState.Connected
                    ? StatusTone.Accent
                    : StatusTone.Warning
            }
        ];
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
