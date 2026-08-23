namespace TruckNav.Connector.App.ViewModels;

public sealed class PageSectionViewModel
{
    public required string Heading { get; init; }

    public required string Description { get; init; }

    public required IReadOnlyList<string> Items { get; init; }
}
