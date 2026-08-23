using TruckNav.Connector.Core.Enums;

namespace TruckNav.Connector.App.ViewModels;

public sealed class FirstTimeSetupViewModel : InfoPageViewModel
{
    public FirstTimeSetupViewModel()
        : base(
            PageId.FirstTimeSetup,
            "First-Time Setup",
            "The normal user experience should be configure once, forget the Connector exists, and drive using the Android GPS.")
    {
        Sections.Add(new PageSectionViewModel
        {
            Heading = "Expected flow",
            Description = "This page will become the onboarding lane shown when no profiles or trusted devices exist yet.",
            Items =
            [
                "- Name the PC device and generate a persistent connector identity.",
                "- Confirm startup, tray, and notification defaults.",
                "- Detect ETS2 or ATS and launch the GPS capture wizard.",
                "- Save a separate crop profile for each supported game."
            ]
        });

        Sections.Add(new PageSectionViewModel
        {
            Heading = "Current shell scope",
            Description = "Phase 1 establishes the information architecture and explicit state flow only.",
            Items =
            [
                "- No fake pairing flow has been introduced.",
                "- No manual IP setup is part of the UX.",
                "- The approved HTML mockups are still required for final layout fidelity."
            ]
        });
    }
}
