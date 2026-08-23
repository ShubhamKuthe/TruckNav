using TruckNav.Connector.Core.Enums;

namespace TruckNav.Connector.App.ViewModels;

public sealed class GpsCaptureViewModel : InfoPageViewModel
{
    public GpsCaptureViewModel()
        : base(
            PageId.GpsCapture,
            "GPS Capture",
            "The capture wizard stores normalized Route Advisor crop profiles per game and adapts carefully when resolutions change.")
    {
        Sections.Add(new PageSectionViewModel
        {
            Heading = "Wizard stages",
            Description = "The production wizard flow is already locked in by the specification.",
            Items =
            [
                "- 1. Choose Game",
                "- 2. Select Area",
                "- 3. Preview",
                "- 4. Save"
            ]
        });

        Sections.Add(new PageSectionViewModel
        {
            Heading = "Capture strategy",
            Description = "TruckNav captures real game pixels only. It never invents hidden or synthetic map content.",
            Items =
            [
                "- Prefer the detected game window as the source.",
                "- Store normalized crop coordinates with reference resolution metadata.",
                "- Warn on material resolution changes without deleting the saved profile."
            ]
        });
    }
}
