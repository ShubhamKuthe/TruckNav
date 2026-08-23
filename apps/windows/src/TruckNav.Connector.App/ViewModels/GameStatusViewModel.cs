using System.Windows.Input;

namespace TruckNav.Connector.App.ViewModels;

public sealed class GameStatusViewModel
{
    public required string Name { get; init; }

    public bool IsRunning { get; init; }

    public bool IsConfigured { get; init; }

    public string ActionText { get; init; } = "Configure GPS";

    public bool IsActionEnabled { get; init; }

    public ICommand? ActionCommand { get; init; }

    public string RunningLabel => IsRunning ? "Running" : "Not running";

    public string ConfigurationLabel => IsConfigured ? "Configured" : "Not configured";

    public StatusTone RunningTone => IsRunning ? StatusTone.Success : StatusTone.Neutral;

    public StatusTone ConfigurationTone => IsConfigured ? StatusTone.Success : StatusTone.Neutral;

    public string Detail =>
        IsRunning
            ? "Ready to configure the GPS area for this game."
            : $"Start {Name} to configure its GPS area.";
}
