using CommunityToolkit.Mvvm.ComponentModel;
using TruckNav.Connector.Core.Enums;

namespace TruckNav.Connector.App.ViewModels;

public sealed partial class NavigationItemViewModel : ObservableObject
{
    public NavigationItemViewModel(PageId pageId, string title, string description)
    {
        PageId = pageId;
        Title = title;
        Description = description;
    }

    public PageId PageId { get; }

    public string Title { get; }

    public string Description { get; }

    [ObservableProperty]
    private bool isSelected;
}
