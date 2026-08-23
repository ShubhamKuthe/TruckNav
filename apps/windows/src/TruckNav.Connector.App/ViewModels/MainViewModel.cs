using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using TruckNav.Connector.Core.Enums;
using TruckNav.Connector.Networking.Session;

namespace TruckNav.Connector.App.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    private readonly Dictionary<PageId, PageViewModelBase> _pages;
    private readonly IConnectorSessionStore _sessionStore;

    public MainViewModel(
        HomeViewModel home,
        FirstTimeSetupViewModel firstTimeSetup,
        DevicesViewModel devices,
        GamesViewModel games,
        GpsCaptureViewModel gpsCapture,
        SettingsViewModel settings,
        DiagnosticsViewModel diagnostics,
        IConnectorSessionStore sessionStore)
    {
        _sessionStore = sessionStore;
        _pages = new Dictionary<PageId, PageViewModelBase>
        {
            [PageId.Home] = home,
            [PageId.FirstTimeSetup] = firstTimeSetup,
            [PageId.Devices] = devices,
            [PageId.Games] = games,
            [PageId.GpsCapture] = gpsCapture,
            [PageId.Settings] = settings,
            [PageId.Diagnostics] = diagnostics
        };

        NavigationItems = new ObservableCollection<NavigationItemViewModel>
        {
            new(PageId.FirstTimeSetup, "First-Time Setup", "Onboarding, naming, startup, and the first capture profile."),
            new(PageId.Home, "Home", "The four top-level product states: mobile, game, capture, stream."),
            new(PageId.Devices, "Devices", "Known clients, trust, and one-active-client constraints."),
            new(PageId.Games, "Games", "Supported process detection and restart behavior."),
            new(PageId.GpsCapture, "GPS Capture", "Capture source selection, crop profiles, and resolution change handling."),
            new(PageId.Settings, "Settings", "Startup, notifications, auto-stream, and control mapping."),
            new(PageId.Diagnostics, "Advanced / Diagnostics", "Connection details, stream stats, logs, and troubleshooting data.")
        };

        ConnectorName = _sessionStore.Snapshot.LocalConnector.DeviceName;
        FooterSummary = "Repo bootstrapped on August 23, 2026 with .NET 8 WPF and Flutter 3.44.4 shells. Final visual fidelity is blocked on the missing approved HTML design references.";
        ApplySessionSnapshot(_sessionStore.Snapshot);
        _sessionStore.SnapshotChanged += (_, snapshot) => Dispatch(() => ApplySessionSnapshot(snapshot));

        SelectedNavigationItem = NavigationItems[1];
    }

    public string ConnectorName { get; }

    public string FooterSummary { get; }

    [ObservableProperty]
    private StatusTone readyState;

    [ObservableProperty]
    private string readySummary = string.Empty;

    public ObservableCollection<NavigationItemViewModel> NavigationItems { get; }

    [ObservableProperty]
    private NavigationItemViewModel? selectedNavigationItem;

    [ObservableProperty]
    private PageViewModelBase currentPage = null!;

    partial void OnSelectedNavigationItemChanged(NavigationItemViewModel? value)
    {
        foreach (var item in NavigationItems)
        {
            item.IsSelected = item == value;
        }

        if (value is not null && _pages.TryGetValue(value.PageId, out var page))
        {
            CurrentPage = page;
        }
    }

    private void ApplySessionSnapshot(ConnectorSessionSnapshot snapshot)
    {
        switch (snapshot.DeviceState)
        {
            case DeviceConnectionState.Connected:
                ReadyState = StatusTone.Success;
                ReadySummary = "CONNECTED";
                break;
            case DeviceConnectionState.Reconnecting:
                ReadyState = StatusTone.Accent;
                ReadySummary = "RECONNECTING";
                break;
            default:
                ReadyState = StatusTone.Accent;
                ReadySummary = "READY";
                break;
        }
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
