using CommunityToolkit.Mvvm.ComponentModel;
using TruckNav.Connector.Core.Enums;

namespace TruckNav.Connector.App.ViewModels;

public abstract class PageViewModelBase : ObservableObject
{
    protected PageViewModelBase(PageId pageId, string title)
    {
        PageId = pageId;
        Title = title;
    }

    public PageId PageId { get; }

    public string Title { get; }
}
