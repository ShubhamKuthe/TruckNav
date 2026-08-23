using TruckNav.Connector.Core.Enums;

namespace TruckNav.Connector.App.ViewModels;

public sealed class GamesViewModel : InfoPageViewModel
{
    public GamesViewModel()
        : base(
            PageId.Games,
            "Games",
            "TruckNav watches supported SCS game processes and propagates that state across the app and to mobile clients.")
    {
        Sections.Add(new PageSectionViewModel
        {
            Heading = "Supported executables",
            Description = "Diagnostics may show process details, but the normal UX should remain human-readable.",
            Items =
            [
                "- ETS2: eurotrucks2.exe",
                "- ATS: amtrucks.exe",
                "- GameType.None remains a normal waiting state."
            ]
        });

        Sections.Add(new PageSectionViewModel
        {
            Heading = "Restart behavior",
            Description = "The session should survive game restarts without forcing the mobile device to reconnect.",
            Items =
            [
                "- Connected device stays connected when the game closes.",
                "- Stream transitions to Waiting for Game.",
                "- Capture is revalidated when the game relaunches."
            ]
        });
    }
}
