using System.Windows.Input;
using TruckNav.Connector.Core.Enums;

namespace TruckNav.Connector.App.ViewModels;

public sealed class GpsGameChoiceViewModel
{
    public required GameType GameType { get; init; }
    public required string Name { get; init; }
    public required bool IsRunning { get; init; }
    public required bool IsWindowAvailable { get; init; }
    public required bool IsConfigured { get; init; }
    public required ICommand UseCommand { get; init; }

    public string RunningLabel => IsWindowAvailable
        ? "Running"
        : IsRunning
            ? "Window still loading"
            : "Not running";

    public string Detail => IsWindowAvailable
        ? "The game window is ready to capture."
        : IsRunning
            ? "Wait for the game window, then check again."
            : $"Start {Name} to configure its GPS area.";

    public string ConfigurationLabel => IsConfigured ? "GPS area configured" : "Not configured";
}
