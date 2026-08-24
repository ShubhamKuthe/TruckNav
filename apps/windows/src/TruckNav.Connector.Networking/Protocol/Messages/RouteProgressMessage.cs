namespace TruckNav.Connector.Networking.Protocol.Messages;

public sealed record RouteProgressMessage(
    string RouteId,
    int SegmentIndex,
    double ProjectedX,
    double ProjectedZ,
    double DistanceFromRouteMeters,
    double RemainingDistanceMeters,
    double RemainingTimeSeconds,
    string NextManeuver,
    string NextInstruction,
    double DistanceToNextManeuverMeters,
    bool IsOffRoute,
    bool TeleportDetected,
    DateTimeOffset TimestampUtc)
    : MessageBase(Protocol.ProtocolMessageType.RouteProgress);
