using TruckNav.Connector.Core.Enums;

namespace TruckNav.Connector.App.ViewModels;

public sealed class GpsCaptureViewModel : PageViewModelBase
{
    public GpsCaptureViewModel()
        : base(PageId.GpsCapture, "GPS Capture")
    {
    }

    public string StepSummary => "① Choose Game — ② Select Area — ③ Preview — ④ Save";
}
