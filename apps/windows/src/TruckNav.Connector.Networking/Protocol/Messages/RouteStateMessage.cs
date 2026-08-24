namespace TruckNav.Connector.Networking.Protocol.Messages;

public sealed record RoutePointMessage(double X, double Z);

public sealed record RouteStepMessage(
    string Maneuver,
    string Instruction,
    int PointIndex,
    double DistanceFromStartMeters);

public sealed record RouteContextRoadMessage(
    double FromX,
    double FromZ,
    double ToX,
    double ToZ,
    bool IsMajor,
    bool IsFerry);

public sealed record RouteMapPointMessage(
    string Type,
    double X,
    double Z,
    string? Label,
    bool IsDestination);

public sealed record RouteMapLabelMessage(
    string Text,
    double X,
    double Z,
    bool IsDestination);

public sealed record RouteStateMessage(
    long Revision,
    bool Available,
    string? RouteId,
    string? Game,
    string? Destination,
    IReadOnlyList<RoutePointMessage> Points,
    IReadOnlyList<RouteStepMessage> Steps,
    IReadOnlyList<RouteContextRoadMessage> ContextRoads,
    IReadOnlyList<RouteMapPointMessage> MapPoints,
    IReadOnlyList<RouteMapLabelMessage> MapLabels,
    double TotalDistanceMeters,
    double EstimatedTimeSeconds,
    DateTimeOffset? CalculatedUtc,
    bool IsReroute)
    : MessageBase(Protocol.ProtocolMessageType.RouteState);
