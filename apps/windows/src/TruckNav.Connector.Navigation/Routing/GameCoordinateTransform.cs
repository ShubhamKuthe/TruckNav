using TruckNav.Connector.Core.Interfaces;
using TruckNav.Connector.Core.Models;

namespace TruckNav.Connector.Navigation.Routing;

public sealed class GameCoordinateTransform : IGameCoordinateTransform
{
    public WorldPoint ToMapPoint(TelemetrySnapshot telemetry) => telemetry.Position;
}
