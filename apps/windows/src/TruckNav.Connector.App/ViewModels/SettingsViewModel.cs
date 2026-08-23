using TruckNav.Connector.Core.Enums;

namespace TruckNav.Connector.App.ViewModels;

public sealed class SettingsViewModel : InfoPageViewModel
{
    public SettingsViewModel()
        : base(
            PageId.Settings,
            "Settings",
            "Settings gather startup behavior, auto-stream defaults, connection UX, command remapping, and future update plumbing.")
    {
        Sections.Add(new PageSectionViewModel
        {
            Heading = "Key toggles",
            Description = "These settings are specified even before their backing services are fully implemented.",
            Items =
            [
                "- Launch at Windows startup",
                "- Start streaming automatically",
                "- Connection notifications",
                "- Game detection notifications",
                "- Streaming notifications"
            ]
        });

        Sections.Add(new PageSectionViewModel
        {
            Heading = "Control mapping",
            Description = "Android sends semantic commands only. Windows owns the local input mapping.",
            Items =
            [
                "- zoom_in",
                "- zoom_out",
                "- cycle_navigation_view"
            ]
        });
    }
}
