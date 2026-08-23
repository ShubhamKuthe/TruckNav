using TruckNav.Connector.Core.Models;

namespace TruckNav.Connector.Capture;

public interface IWindowsMonitorProvider
{
    IReadOnlyList<CaptureSource> GetMonitorSources();
}
