using TruckNav.Connector.Core.Enums;
using TruckNav.Connector.Core.Models;

namespace TruckNav.Connector.Core.Interfaces;

public interface IGpsCapturePresetProvider
{
    NormalizedRegion? GetStandardRouteAdvisorPreset(GameType gameType, int sourceWidth, int sourceHeight);
}
