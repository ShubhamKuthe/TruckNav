using System.Windows.Input;

namespace TruckNav.Connector.App.ViewModels;

public sealed class DeviceEntryViewModel
{
    public required string Name { get; init; }

    public required string Subtitle { get; init; }

    public required string StatusText { get; init; }

    public required StatusTone Tone { get; init; }

    public string ActionText { get; init; } = string.Empty;

    public bool IsPrimaryAction { get; init; }

    public bool IsActionEnabled { get; init; } = true;

    public ICommand? ActionCommand { get; init; }

    public bool HasAction => !string.IsNullOrWhiteSpace(ActionText) && ActionCommand is not null;
}
