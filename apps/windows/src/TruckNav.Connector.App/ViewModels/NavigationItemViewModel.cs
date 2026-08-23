using CommunityToolkit.Mvvm.ComponentModel;
using TruckNav.Connector.Core.Enums;

namespace TruckNav.Connector.App.ViewModels;

public sealed partial class NavigationItemViewModel : ObservableObject
{
    public NavigationItemViewModel(PageId pageId, string title, string glyph)
    {
        PageId = pageId;
        Title = title;
        Glyph = glyph;
    }

    public PageId PageId { get; }

    public string Title { get; }

    public string Glyph { get; }

    [ObservableProperty]
    private bool isSelected;
}
