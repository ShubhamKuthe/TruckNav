using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using TruckNav.Connector.App.Services;
using TruckNav.Connector.Core.Enums;
using TruckNav.Connector.Networking.Session;

namespace TruckNav.Connector.App.ViewModels;

public sealed class HomeViewModel : PageViewModelBase
{
    private readonly INavigationService _navigationService;
    private readonly RelayCommand _disabledCommand = new(() => { });

    public HomeViewModel(
        IConnectorSessionStore sessionStore,
        INavigationService navigationService)
        : base(PageId.Home, "Home")
    {
        _navigationService = navigationService;
        Cards = new ObservableCollection<HomeCardViewModel>();

        RefreshCards(sessionStore.Snapshot);
        sessionStore.SnapshotChanged += (_, snapshot) => Dispatch(() => RefreshCards(snapshot));
    }

    public ObservableCollection<HomeCardViewModel> Cards { get; }

    private void RefreshCards(ConnectorSessionSnapshot snapshot)
    {
        Cards.Clear();

        foreach (var card in CreateCards(snapshot))
        {
            Cards.Add(card);
        }
    }

    private IReadOnlyList<HomeCardViewModel> CreateCards(ConnectorSessionSnapshot snapshot)
    {
        var mobileName = snapshot.ActiveMobileDevice?.DeviceName ?? snapshot.LastSeenMobileDevice?.DeviceName ?? "No mobile device";
        var mobilePlatform = FormatPlatform(snapshot.ActiveMobileDevice?.Platform ?? snapshot.LastSeenMobileDevice?.Platform);

        var mobileCard = snapshot.DeviceState switch
        {
            DeviceConnectionState.Connected => new HomeCardViewModel
            {
                IconGlyph = "\uE8EA",
                Title = "Mobile Device",
                PrimaryText = mobileName,
                SecondaryText = mobilePlatform,
                StatusText = "Paired",
                Tone = StatusTone.Success,
                UseMonospacePrimary = true
            },
            DeviceConnectionState.Reconnecting => new HomeCardViewModel
            {
                IconGlyph = "\uE8EA",
                Title = "Mobile Device",
                PrimaryText = mobileName,
                SecondaryText = mobilePlatform,
                StatusText = "Waiting to reconnect",
                Tone = StatusTone.Accent,
                ActionText = "View Devices",
                ActionCommand = new RelayCommand(() => _navigationService.NavigateTo(PageId.Devices)),
                UseMonospacePrimary = true
            },
            _ => new HomeCardViewModel
            {
                IconGlyph = "\uE8EA",
                Title = "Mobile Device",
                PrimaryText = "Waiting for a device",
                SecondaryText = "Open TruckNav on your phone and it will appear here.",
                StatusText = "Ready to pair",
                Tone = StatusTone.Accent,
                ActionText = "View Devices",
                ActionCommand = new RelayCommand(() => _navigationService.NavigateTo(PageId.Devices))
            }
        };

        var streamState = snapshot.DeviceState switch
        {
            DeviceConnectionState.Connected => "Waiting for game",
            DeviceConnectionState.Reconnecting => $"Waiting for {mobileName}",
            _ => "Waiting for mobile device"
        };

        var streamDetail = snapshot.DeviceState switch
        {
            DeviceConnectionState.Connected =>
                "Streaming starts automatically once everything is ready.",
            DeviceConnectionState.Reconnecting =>
                "Streaming will resume automatically when the device reconnects.",
            _ =>
                "Streaming starts automatically once everything is ready."
        };

        return
        [
            mobileCard,
            new HomeCardViewModel
            {
                IconGlyph = "\uE7FC",
                Title = "Game",
                PrimaryText = "No supported game running",
                SecondaryText = "Start Euro Truck Simulator 2 or American Truck Simulator.",
                Tone = StatusTone.Neutral
            },
            new HomeCardViewModel
            {
                IconGlyph = "\uE707",
                Title = "GPS Capture",
                PrimaryText = "GPS area not configured",
                SecondaryText = "Select the navigation area you want to send to TruckNav.",
                Tone = StatusTone.Warning,
                ActionText = "Configure GPS Area",
                ActionCommand = new RelayCommand(() => _navigationService.NavigateTo(PageId.GpsCapture)),
                IsActionPrimary = true,
                UseToneForPrimary = true
            },
            new HomeCardViewModel
            {
                IconGlyph = "\uE8BD",
                Title = "Stream",
                PrimaryText = streamState,
                SecondaryText = streamDetail,
                Tone = snapshot.DeviceState == DeviceConnectionState.Reconnecting
                    ? StatusTone.Accent
                    : StatusTone.Neutral
            }
        ];
    }

    private static string FormatPlatform(string? platform) =>
        string.IsNullOrWhiteSpace(platform)
            ? "Android"
            : char.ToUpperInvariant(platform[0]) + platform[1..].ToLowerInvariant();

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
