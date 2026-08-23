using TruckNav.Connector.Core.Enums;

namespace TruckNav.Connector.App.ViewModels;

public abstract class PageViewModelBase
{
    protected PageViewModelBase(PageId pageId, string title, string description)
    {
        PageId = pageId;
        Title = title;
        Description = description;
    }

    public PageId PageId { get; }

    public string Title { get; }

    public string Description { get; }
}
