using TruckNav.Connector.Core.Enums;

namespace TruckNav.Connector.App.Services;

public interface INavigationService
{
    PageId CurrentPageId { get; }

    event EventHandler<PageId>? CurrentPageChanged;

    void NavigateTo(PageId pageId);
}
