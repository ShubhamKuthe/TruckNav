using System.Collections.ObjectModel;
using TruckNav.Connector.Core.Enums;

namespace TruckNav.Connector.App.ViewModels;

public sealed class GamesViewModel : PageViewModelBase
{
    public GamesViewModel()
        : base(PageId.Games, "Games")
    {
        Games =
        [
            new GameStatusViewModel
            {
                Name = "Euro Truck Simulator 2",
                IsRunning = false,
                IsConfigured = false,
                IsActionEnabled = false
            },
            new GameStatusViewModel
            {
                Name = "American Truck Simulator",
                IsRunning = false,
                IsConfigured = false,
                IsActionEnabled = false
            }
        ];
    }

    public ObservableCollection<GameStatusViewModel> Games { get; }
}
