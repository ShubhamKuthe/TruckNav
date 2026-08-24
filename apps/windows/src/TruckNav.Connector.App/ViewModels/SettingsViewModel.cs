using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TruckNav.Connector.App.Services;
using TruckNav.Connector.Core.Enums;
using TruckNav.Connector.Networking;
using TruckNav.Connector.Networking.Session;

namespace TruckNav.Connector.App.ViewModels;

public sealed partial class SettingsViewModel : PageViewModelBase
{
    private readonly IConnectorUiStateStore _uiStateStore;
    private readonly INavigationService _navigationService;
    private readonly ILocalConnectorProfileService _profileService;
    private bool _isLoaded;

    public SettingsViewModel(
        IConnectorUiStateStore uiStateStore,
        INavigationService navigationService,
        ILocalConnectorProfileService profileService,
        IConnectorSessionStore sessionStore)
        : base(PageId.Settings, "Settings")
    {
        _uiStateStore = uiStateStore;
        _navigationService = navigationService;
        _profileService = profileService;

        var state = uiStateStore.CurrentState;
        PcName = sessionStore.Snapshot.LocalConnector.DeviceName;
        EditablePcName = PcName;
        ApplyState(state);
        _isLoaded = true;

        sessionStore.SnapshotChanged += (_, snapshot) => Dispatch(() =>
        {
            PcName = snapshot.LocalConnector.DeviceName;
            EditablePcName = snapshot.LocalConnector.DeviceName;
        });
    }

    public string VersionLabel => $"Version {TruckNavNetworkDefaults.AppVersion}";

    [ObservableProperty]
    private string pcName = string.Empty;

    [ObservableProperty]
    private string editablePcName = string.Empty;

    [ObservableProperty]
    private bool startWithWindows;

    [ObservableProperty]
    private bool startMinimized;

    [ObservableProperty]
    private bool minimizeToTray;

    [ObservableProperty]
    private bool closeButtonMinimizesToTray;

    [ObservableProperty]
    private bool allowAutomaticReconnect;

    [ObservableProperty]
    private bool rememberPreviousDevices;

    [ObservableProperty]
    private bool startNavigationAutomatically;

    [ObservableProperty]
    private bool routeRenderingAcceleration;

    [ObservableProperty]
    private bool showConnectionNotifications;

    [ObservableProperty]
    private bool showGameDetectionNotifications;

    [ObservableProperty]
    private bool showNavigationNotifications;

    public bool CanSavePcName =>
        !string.IsNullOrWhiteSpace(EditablePcName) &&
        !string.Equals(EditablePcName.Trim(), PcName, StringComparison.Ordinal);

    [RelayCommand]
    private void OpenDiagnostics() => _navigationService.NavigateTo(PageId.Diagnostics);

    [RelayCommand(CanExecute = nameof(CanSavePcName))]
    private void SavePcName()
    {
        _profileService.SaveDeviceName(EditablePcName);
        EditablePcName = PcName;
        SavePcNameCommand.NotifyCanExecuteChanged();
    }

    partial void OnEditablePcNameChanged(string value) => SavePcNameCommand.NotifyCanExecuteChanged();
    partial void OnStartWithWindowsChanged(bool value) => PersistState();
    partial void OnStartMinimizedChanged(bool value) => PersistState();
    partial void OnMinimizeToTrayChanged(bool value) => PersistState();
    partial void OnCloseButtonMinimizesToTrayChanged(bool value) => PersistState();
    partial void OnAllowAutomaticReconnectChanged(bool value) => PersistState();
    partial void OnRememberPreviousDevicesChanged(bool value) => PersistState();
    partial void OnStartNavigationAutomaticallyChanged(bool value) => PersistState();
    partial void OnRouteRenderingAccelerationChanged(bool value) => PersistState();
    partial void OnShowConnectionNotificationsChanged(bool value) => PersistState();
    partial void OnShowGameDetectionNotificationsChanged(bool value) => PersistState();
    partial void OnShowNavigationNotificationsChanged(bool value) => PersistState();

    private void ApplyState(ConnectorUiState state)
    {
        StartWithWindows = state.StartWithWindows;
        StartMinimized = state.StartMinimized;
        MinimizeToTray = state.MinimizeToTray;
        CloseButtonMinimizesToTray = state.CloseButtonMinimizesToTray;
        AllowAutomaticReconnect = state.AllowAutomaticReconnect;
        RememberPreviousDevices = state.RememberPreviousDevices;
        StartNavigationAutomatically = state.StartNavigationAutomatically;
        RouteRenderingAcceleration = state.RouteRenderingAcceleration;
        ShowConnectionNotifications = state.ShowConnectionNotifications;
        ShowGameDetectionNotifications = state.ShowGameDetectionNotifications;
        ShowNavigationNotifications = state.ShowNavigationNotifications;
    }

    private void PersistState()
    {
        if (!_isLoaded)
        {
            return;
        }

        _uiStateStore.SaveState(_uiStateStore.CurrentState with
        {
            StartWithWindows = StartWithWindows,
            StartMinimized = StartMinimized,
            MinimizeToTray = MinimizeToTray,
            CloseButtonMinimizesToTray = CloseButtonMinimizesToTray,
            AllowAutomaticReconnect = AllowAutomaticReconnect,
            RememberPreviousDevices = RememberPreviousDevices,
            StartNavigationAutomatically = StartNavigationAutomatically,
            RouteRenderingAcceleration = RouteRenderingAcceleration,
            ShowConnectionNotifications = ShowConnectionNotifications,
            ShowGameDetectionNotifications = ShowGameDetectionNotifications,
            ShowNavigationNotifications = ShowNavigationNotifications
        });
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
