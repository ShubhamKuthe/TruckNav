namespace TruckNav.Connector.App.ViewModels;

public sealed class StatusCardViewModel
{
    public required string Title { get; init; }

    public required string State { get; init; }

    public required string Detail { get; init; }

    public required string Hint { get; init; }

    public required StatusTone Tone { get; init; }
}
