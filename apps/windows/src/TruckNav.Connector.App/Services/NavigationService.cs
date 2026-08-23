using TruckNav.Connector.Core.Enums;

namespace TruckNav.Connector.App.Services;

public sealed class NavigationService : INavigationService
{
    private PageId _currentPageId = PageId.Home;

    public PageId CurrentPageId => _currentPageId;

    public event EventHandler<PageId>? CurrentPageChanged;

    public void NavigateTo(PageId pageId)
    {
        if (_currentPageId == pageId)
        {
            return;
        }

        _currentPageId = pageId;
        CurrentPageChanged?.Invoke(this, pageId);
    }
}
