using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TruckNav.Connector.App.Services;
using TruckNav.Connector.Core.Enums;
using TruckNav.Connector.Networking;
using TruckNav.Connector.Networking.Session;

namespace TruckNav.Connector.App.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    private readonly Dictionary<PageId, PageViewModelBase> _pages;
    private readonly INavigationService _navigationService;

    public MainViewModel(
        HomeViewModel home,
        FirstTimeSetupViewModel firstTimeSetup,
        DevicesViewModel devices,
        GamesViewModel games,
        GpsCaptureViewModel gpsCapture,
        SettingsViewModel settings,
        DiagnosticsViewModel diagnostics,
        IConnectorSessionStore sessionStore,
        IConnectorUiStateStore uiStateStore,
        INavigationService navigationService)
    {
        _navigationService = navigationService;
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
            new(PageId.Home, "Home", "\uE80F"),
            new(PageId.Devices, "Devices", "\uE7F4"),
            new(PageId.Games, "Games", "\uE7FC"),
            new(PageId.GpsCapture, "GPS Capture", "\uE707"),
            new(PageId.Settings, "Settings", "\uE713")
        };

        ApplySessionSnapshot(sessionStore.Snapshot);
        sessionStore.SnapshotChanged += (_, snapshot) => Dispatch(() => ApplySessionSnapshot(snapshot));
        _navigationService.CurrentPageChanged += (_, pageId) => Dispatch(() => ApplyCurrentPage(pageId));

        var startPage = uiStateStore.CurrentState.HasCompletedOnboarding
            ? PageId.Home
            : PageId.FirstTimeSetup;

        ApplyCurrentPage(startPage);
        _navigationService.NavigateTo(startPage);
    }

    public ObservableCollection<NavigationItemViewModel> NavigationItems { get; }

    public string VersionLabel => $"v{TruckNavNetworkDefaults.AppVersion}";

    [ObservableProperty]
    private string connectorName = string.Empty;

    [ObservableProperty]
    private StatusTone readyState = StatusTone.Success;

    [ObservableProperty]
    private string readySummary = "Ready";

    [ObservableProperty]
    private bool isOnboardingVisible;

    [ObservableProperty]
    private bool isShellVisible = true;

    [ObservableProperty]
    private PageViewModelBase currentPage = null!;

    [RelayCommand]
    private void NavigateToPage(PageId pageId) => _navigationService.NavigateTo(pageId);

    private void ApplySessionSnapshot(ConnectorSessionSnapshot snapshot)
    {
        ConnectorName = snapshot.LocalConnector.DeviceName;

        switch (snapshot.DeviceState)
        {
            case DeviceConnectionState.Connected:
                ReadyState = StatusTone.Success;
                ReadySummary = "Paired";
                break;
            case DeviceConnectionState.Reconnecting:
                ReadyState = StatusTone.Accent;
                ReadySummary = "Reconnecting";
                break;
            default:
                ReadyState = StatusTone.Success;
                ReadySummary = "Ready";
                break;
        }
    }

    private void ApplyCurrentPage(PageId pageId)
    {
        if (!_pages.TryGetValue(pageId, out var page))
        {
            return;
        }

        CurrentPage = page;
        IsOnboardingVisible = pageId == PageId.FirstTimeSetup;
        IsShellVisible = !IsOnboardingVisible;

        var selectedPageId = pageId == PageId.Diagnostics
            ? PageId.Settings
            : pageId;

        foreach (var item in NavigationItems)
        {
            item.IsSelected = IsShellVisible && item.PageId == selectedPageId;
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
