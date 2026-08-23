using System.Collections.ObjectModel;
using TruckNav.Connector.Core.Enums;

namespace TruckNav.Connector.App.ViewModels;

public abstract class InfoPageViewModel : PageViewModelBase
{
    protected InfoPageViewModel(PageId pageId, string title, string description)
        : base(pageId, title, description)
    {
        Sections = new ObservableCollection<PageSectionViewModel>();
    }

    public ObservableCollection<PageSectionViewModel> Sections { get; }
}
