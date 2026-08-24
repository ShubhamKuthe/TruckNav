using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using TruckNav.Connector.Core.Interfaces;
using TruckNav.Connector.Core.Enums;
using TruckNav.Connector.Core.Utilities;

namespace TruckNav.Connector.App.ViewModels;

public sealed class GamesViewModel : PageViewModelBase
{
    public GamesViewModel(
        IGameInstallationLocator installationLocator,
        ITelemetryPluginInstaller pluginInstaller,
        INavigationDataBuilder dataBuilder)
        : base(PageId.Games, "Games")
    {
        Games = new ObservableCollection<GameStatusViewModel>(
            SupportedGameCatalog.All.Select(definition => new GameStatusViewModel(
                definition.GameType,
                installationLocator,
                pluginInstaller,
                dataBuilder)));
        RefreshAllCommand = new AsyncRelayCommand(RefreshAllAsync);
        _ = RefreshAllAsync();
    }

    public ObservableCollection<GameStatusViewModel> Games { get; }

    public IAsyncRelayCommand RefreshAllCommand { get; }

    private Task RefreshAllAsync() => Task.WhenAll(Games.Select(game => game.RefreshAsync()));
}
