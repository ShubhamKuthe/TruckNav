using System.Windows.Input;

namespace TruckNav.Connector.App.ViewModels;

public sealed class HomeCardViewModel
{
    public required string IconGlyph { get; init; }

    public required string Title { get; init; }

    public required string PrimaryText { get; init; }

    public string SecondaryText { get; init; } = string.Empty;

    public string StatusText { get; init; } = string.Empty;

    public required StatusTone Tone { get; init; }

    public string ActionText { get; init; } = string.Empty;

    public bool IsActionPrimary { get; init; }

    public bool IsActionEnabled { get; init; } = true;

    public bool UseMonospacePrimary { get; init; }

    public bool UseToneForPrimary { get; init; }

    public ICommand? ActionCommand { get; init; }

    public bool HasStatus => !string.IsNullOrWhiteSpace(StatusText);

    public bool HasAction => !string.IsNullOrWhiteSpace(ActionText) && ActionCommand is not null;
}
