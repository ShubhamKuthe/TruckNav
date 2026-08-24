using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using TruckNav.Connector.App.Services;
using TruckNav.Connector.Core.Enums;
using TruckNav.Connector.Core.Interfaces;
using TruckNav.Connector.Core.Models;
using TruckNav.Connector.Core.Utilities;
using TruckNav.Connector.Networking.Session;

namespace TruckNav.Connector.App.ViewModels;

public sealed class HomeViewModel : PageViewModelBase
{
    private readonly INavigationService _navigationService;
    private readonly IConnectorSessionStore _sessionStore;
    private readonly INavigationStateStore _navigationStateStore;

    public HomeViewModel(
        IConnectorSessionStore sessionStore,
        INavigationStateStore navigationStateStore,
        INavigationService navigationService)
        : base(PageId.Home, "Home")
    {
        _sessionStore = sessionStore;
        _navigationStateStore = navigationStateStore;
        _navigationService = navigationService;
        Cards = new ObservableCollection<HomeCardViewModel>();

        RefreshCards();
        sessionStore.SnapshotChanged += (_, _) => Dispatch(RefreshCards);
        navigationStateStore.SnapshotChanged += (_, _) => Dispatch(RefreshCards);
    }

    public ObservableCollection<HomeCardViewModel> Cards { get; }

    private void RefreshCards()
    {
        Cards.Clear();
        foreach (var card in CreateCards(_sessionStore.Snapshot, _navigationStateStore.Snapshot))
        {
            Cards.Add(card);
        }
    }

    private IReadOnlyList<HomeCardViewModel> CreateCards(
        ConnectorSessionSnapshot session,
        NavigationSnapshot navigation)
    {
        var mobileName = session.ActiveMobileDevice?.DeviceName ?? session.LastSeenMobileDevice?.DeviceName;
        var mobile = session.DeviceState == DeviceConnectionState.Connected
            ? new HomeCardViewModel
            {
                IconGlyph = "\uE8EA",
                Title = "Mobile Device",
                PrimaryText = mobileName ?? "Android device",
                SecondaryText = "Native route and telemetry channel connected.",
                StatusText = "Paired",
                Tone = StatusTone.Success,
                UseMonospacePrimary = true
            }
            : new HomeCardViewModel
            {
                IconGlyph = "\uE8EA",
                Title = "Mobile Device",
                PrimaryText = mobileName ?? "Waiting for a device",
                SecondaryText = "Open TruckNav on Android to pair over the local network.",
                StatusText = session.DeviceState == DeviceConnectionState.Reconnecting ? "Reconnecting" : "Ready to pair",
                Tone = session.DeviceState == DeviceConnectionState.Reconnecting ? StatusTone.Accent : StatusTone.Neutral,
                ActionText = "View Devices",
                ActionCommand = new RelayCommand(() => _navigationService.NavigateTo(PageId.Devices))
            };

        var telemetry = navigation.Telemetry;
        var game = telemetry.Connected
            ? new HomeCardViewModel
            {
                IconGlyph = "\uE7FC",
                Title = "Game Telemetry",
                PrimaryText = SupportedGameCatalog.GetDisplayName(telemetry.Game),
                SecondaryText = $"{telemetry.SpeedKph:0} km/h  |  X {telemetry.Position.X:0} Z {telemetry.Position.Z:0}",
                StatusText = telemetry.Paused ? "Paused" : "Live",
                Tone = telemetry.Paused ? StatusTone.Warning : StatusTone.Success
            }
            : new HomeCardViewModel
            {
                IconGlyph = "\uE7FC",
                Title = "Game Telemetry",
                PrimaryText = "Waiting for ETS2 or ATS",
                SecondaryText = "Install the plugin, then start a supported game.",
                Tone = StatusTone.Neutral,
                ActionText = "Set Up Games",
                ActionCommand = new RelayCommand(() => _navigationService.NavigateTo(PageId.Games))
            };

        var route = navigation.Route;
        var routeCard = new HomeCardViewModel
        {
            IconGlyph = "\uE707",
            Title = "Native Route",
            PrimaryText = route is null ? SplitWords(navigation.Status.ToString()) : route.Destination.DisplayName,
            SecondaryText = route is null
                ? navigation.StatusMessage
                : $"{route.TotalDistanceMeters / 1_000:0.0} km  |  {route.Points.Count:N0} route points",
            StatusText = route is null ? string.Empty : navigation.Progress?.IsOffRoute == true ? "Off route" : "Navigating",
            Tone = route is null ? NavigationTone(navigation.Status) : navigation.Progress?.IsOffRoute == true ? StatusTone.Warning : StatusTone.Success,
            ActionText = "Open Navigation",
            ActionCommand = new RelayCommand(() => _navigationService.NavigateTo(PageId.Navigation)),
            IsActionPrimary = true
        };

        var setupReady = telemetry.Connected || navigation.Status is not NavigationRuntimeStatus.NavigationDataMissing;
        var setup = new HomeCardViewModel
        {
            IconGlyph = "\uE8BD",
            Title = "Local Pipeline",
            PrimaryText = setupReady ? "Local-only navigation" : "Map graph required",
            SecondaryText = "Telemetry and route calculation stay on this PC and your LAN.",
            StatusText = $"Protocol v2  |  route r{navigation.RouteRevision}",
            Tone = setupReady ? StatusTone.Success : StatusTone.Warning,
            ActionText = "Manage Map Data",
            ActionCommand = new RelayCommand(() => _navigationService.NavigateTo(PageId.Games))
        };

        return [mobile, game, routeCard, setup];
    }

    private static StatusTone NavigationTone(NavigationRuntimeStatus status) => status switch
    {
        NavigationRuntimeStatus.NavigationDataMissing or NavigationRuntimeStatus.DestinationUnresolved => StatusTone.Warning,
        NavigationRuntimeStatus.Faulted or NavigationRuntimeStatus.RouteUnavailable => StatusTone.Error,
        NavigationRuntimeStatus.CalculatingRoute or NavigationRuntimeStatus.Rerouting => StatusTone.Accent,
        _ => StatusTone.Neutral
    };

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
