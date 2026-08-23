using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using TruckNav.Connector.App.Services;
using TruckNav.Connector.Core.Enums;
using TruckNav.Connector.Core.Interfaces;
using TruckNav.Connector.Core.Utilities;
using TruckNav.Connector.Networking.Session;

namespace TruckNav.Connector.App.ViewModels;

public sealed class GamesViewModel : PageViewModelBase
{
    private readonly IConnectorSessionStore _sessionStore;
    private readonly IGpsCaptureProfileStore _profileStore;
    private readonly INavigationService _navigationService;

    public GamesViewModel(
        IConnectorSessionStore sessionStore,
        IGpsCaptureProfileStore profileStore,
        INavigationService navigationService)
        : base(PageId.Games, "Games")
    {
        _sessionStore = sessionStore;
        _profileStore = profileStore;
        _navigationService = navigationService;
        Games = new ObservableCollection<GameStatusViewModel>();

        RefreshGames(sessionStore.Snapshot);
        sessionStore.SnapshotChanged += (_, snapshot) => Dispatch(() => RefreshGames(snapshot));
        profileStore.ProfilesChanged += (_, _) =>
            Dispatch(() => RefreshGames(_sessionStore.Snapshot));
    }

    public ObservableCollection<GameStatusViewModel> Games { get; }

    private void RefreshGames(ConnectorSessionSnapshot snapshot)
    {
        Games.Clear();

        foreach (var definition in SupportedGameCatalog.All)
        {
            var runningGame = snapshot.GameDetection.GetGame(definition.GameType);
            Games.Add(new GameStatusViewModel
            {
                Name = definition.DisplayName,
                IsRunning = runningGame?.ProcessRunning == true,
                IsConfigured = _profileStore.Get(definition.GameType) is not null,
                IsActionEnabled = runningGame?.WindowAvailable == true,
                ActionCommand = new RelayCommand(() => _navigationService.NavigateTo(PageId.GpsCapture))
            });
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
