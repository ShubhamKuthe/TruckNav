using TruckNav.Connector.Core.Enums;

namespace TruckNav.Connector.Core.Models;

public sealed record NavigationStep(
    NavigationManeuverType Maneuver,
    string Instruction,
    int PointIndex,
    double DistanceFromStartMeters);

public sealed record ResolvedDestination(
    string CityToken,
    string CompanyToken,
    string DisplayName,
    int NodeIndex,
    WorldPoint Position);

public sealed record RouteContextRoad(
    WorldPoint From,
    WorldPoint To,
    bool IsMajor,
    bool IsFerry);

public sealed record RouteMapPoint(
    NavigationPoiType Type,
    WorldPoint Position,
    string? Label,
    bool IsDestination);

public sealed record RouteMapLabel(
    string Text,
    WorldPoint Position,
    bool IsDestination);

public sealed record RoutePlan(
    string RouteId,
    GameType Game,
    ResolvedDestination Destination,
    IReadOnlyList<WorldPoint> Points,
    IReadOnlyList<double> SegmentDistancesMeters,
    IReadOnlyList<NavigationStep> Steps,
    double TotalDistanceMeters,
    double EstimatedTimeSeconds,
    DateTimeOffset CalculatedUtc,
    bool IsReroute)
{
    public IReadOnlyList<RouteContextRoad> ContextRoads { get; init; } = [];

    public IReadOnlyList<RouteMapPoint> MapPoints { get; init; } = [];

    public IReadOnlyList<RouteMapLabel> MapLabels { get; init; } = [];
}

public sealed record RouteProgressSnapshot(
    string RouteId,
    int SegmentIndex,
    WorldPoint ProjectedPosition,
    double DistanceFromRouteMeters,
    double RemainingDistanceMeters,
    double RemainingTimeSeconds,
    NavigationManeuverType NextManeuver,
    string NextInstruction,
    double DistanceToNextManeuverMeters,
    bool IsOffRoute,
    bool ShouldReroute,
    bool TeleportDetected,
    DateTimeOffset TimestampUtc);
