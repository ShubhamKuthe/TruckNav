using TruckNav.Connector.Core.Enums;
using TruckNav.Connector.Core.Interfaces;
using TruckNav.Connector.Core.Models;

namespace TruckNav.Connector.Capture;

public sealed class StandardGpsCapturePresetProvider : IGpsCapturePresetProvider
{
    public NormalizedRegion? GetStandardRouteAdvisorPreset(
        GameType gameType,
        int sourceWidth,
        int sourceHeight)
    {
        if (gameType is not (GameType.EuroTruckSimulator2 or GameType.AmericanTruckSimulator) ||
            sourceWidth < 1280 ||
            sourceHeight < 720)
        {
            return null;
        }

        var aspectRatio = sourceWidth / (double)sourceHeight;
        if (Math.Abs(aspectRatio - 16d / 9d) > 0.03d)
        {
            return null;
        }

        // SCS's standard Route Advisor is anchored at the lower-right on 16:9 layouts.
        // This is intentionally a preset, not image recognition.
        return new NormalizedRegion(0.745d, 0.575d, 0.235d, 0.385d);
    }
}
