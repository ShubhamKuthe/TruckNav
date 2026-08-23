using TruckNav.Connector.Core.Enums;

namespace TruckNav.Connector.App.ViewModels;

public sealed class DiagnosticsViewModel : InfoPageViewModel
{
    public DiagnosticsViewModel()
        : base(
            PageId.Diagnostics,
            "Advanced / Diagnostics",
            "Diagnostics is where the product can be technical without leaking stack traces into normal user-facing flows.")
    {
        Sections.Add(new PageSectionViewModel
        {
            Heading = "Telemetry lanes",
            Description = "The diagnostics page will eventually surface structured state snapshots and exportable logs.",
            Items =
            [
                "- local IP and peer information",
                "- stream resolution, FPS, bitrate, latency, packet loss, codec",
                "- capture source, reference resolution, crop dimensions",
                "- timestamped activity log"
            ]
        });

        Sections.Add(new PageSectionViewModel
        {
            Heading = "Safety rules",
            Description = "The app should stay honest about unsupported or not-yet-configured capabilities.",
            Items =
            [
                "- No fake update server checks.",
                "- No fake WebRTC claims while transport remains behind an interface.",
                "- No raw exception stack traces in normal UI."
            ]
        });
    }
}
