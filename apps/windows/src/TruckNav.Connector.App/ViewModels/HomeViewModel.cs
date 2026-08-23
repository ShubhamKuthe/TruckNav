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
    private readonly IGpsCaptureProfileStore _profileStore;
    private readonly IConnectorSessionStore _sessionStore;

    public HomeViewModel(
        IConnectorSessionStore sessionStore,
        IGpsCaptureProfileStore profileStore,
        INavigationService navigationService)
        : base(PageId.Home, "Home")
    {
        _sessionStore = sessionStore;
        _profileStore = profileStore;
        _navigationService = navigationService;
        Cards = new ObservableCollection<HomeCardViewModel>();

        RefreshCards(sessionStore.Snapshot);
        sessionStore.SnapshotChanged += (_, snapshot) => Dispatch(() => RefreshCards(snapshot));
        profileStore.ProfilesChanged += (_, _) => Dispatch(() => RefreshCards(_sessionStore.Snapshot));
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
        var runningGames = snapshot.GameDetection.RunningGames
            .OrderBy(game => game.GameType)
            .ToArray();

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

        var gameCard = CreateGameCard(runningGames);
        var streamGameType = runningGames.Length == 0
            ? GameType.None
            : runningGames[0].GameType;
        var activeProfile = streamGameType == GameType.None
            ? null
            : _profileStore.Get(streamGameType);
        var configuredProfiles = _profileStore.GetAll();
        var streamState = snapshot.DeviceState switch
        {
            DeviceConnectionState.Connected => runningGames.Length == 0
                ? "Waiting for game"
                : activeProfile is null
                    ? "GPS area not configured"
                    : "Streaming not started",
            DeviceConnectionState.Reconnecting => $"Waiting for {mobileName}",
            _ => "Waiting for mobile device"
        };

        var streamDetail = snapshot.DeviceState switch
        {
            DeviceConnectionState.Connected =>
                runningGames.Length == 0
                    ? "Start ETS2 or ATS to continue."
                    : activeProfile is null
                        ? $"Set the GPS area for {SupportedGameCatalog.GetDisplayName(streamGameType)}."
                        : "Local capture is configured. Android video transport is not implemented yet.",
            DeviceConnectionState.Reconnecting =>
                "Device reconnection remains available. Android video transport is not implemented yet.",
            _ =>
                "Pair a device to continue. Android video transport is not implemented yet."
        };

        return
        [
            mobileCard,
            gameCard,
            new HomeCardViewModel
            {
                IconGlyph = "\uE707",
                Title = "GPS Capture",
                PrimaryText = GetGpsCapturePrimaryText(activeProfile, configuredProfiles),
                SecondaryText = activeProfile is not null
                    ? $"{SupportedGameCatalog.GetDisplayName(activeProfile.GameType)} uses a saved {activeProfile.ReferenceSourceWidth} x {activeProfile.ReferenceSourceHeight} reference."
                    : "Select the real Route Advisor map pixels you want to use.",
                StatusText = activeProfile is null ? string.Empty : "GPS area configured",
                Tone = activeProfile is null ? StatusTone.Warning : StatusTone.Success,
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
                    : runningGames.Length == 0
                        ? StatusTone.Neutral
                        : StatusTone.Warning
            }
        ];
    }

    private static string GetGpsCapturePrimaryText(
        GpsCaptureProfile? activeProfile,
        IReadOnlyDictionary<GameType, GpsCaptureProfile> configuredProfiles)
    {
        if (activeProfile is not null)
        {
            return $"{SupportedGameCatalog.GetDisplayName(activeProfile.GameType)} configured";
        }

        return configuredProfiles.Count switch
        {
            0 => "GPS area not configured",
            1 => $"{SupportedGameCatalog.GetDisplayName(configuredProfiles.Keys.Single())} configured",
            _ => "ETS2 and ATS configured"
        };
    }

    private static HomeCardViewModel CreateGameCard(
        IReadOnlyList<RunningGameInfo> runningGames)
    {
        if (runningGames.Count == 0)
        {
            return new HomeCardViewModel
            {
                IconGlyph = "\uE7FC",
                Title = "Game",
                PrimaryText = "No supported game running",
                SecondaryText = "Start Euro Truck Simulator 2 or American Truck Simulator.",
                Tone = StatusTone.Neutral
            };
        }

        if (runningGames.Count == 1)
        {
            return new HomeCardViewModel
            {
                IconGlyph = "\uE7FC",
                Title = "Game",
                PrimaryText = SupportedGameCatalog.GetDisplayName(runningGames[0].GameType),
                SecondaryText = runningGames[0].WindowAvailable
                    ? "Running on this PC"
                    : "Running, window still loading",
                StatusText = "Running",
                Tone = StatusTone.Success
            };
        }

        return new HomeCardViewModel
        {
            IconGlyph = "\uE7FC",
            Title = "Game",
            PrimaryText = string.Join(
                Environment.NewLine,
                runningGames.Select(game => SupportedGameCatalog.GetDisplayName(game.GameType))),
            SecondaryText = "Both supported games are running on this PC.",
            StatusText = "Running",
            Tone = StatusTone.Success
        };
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
