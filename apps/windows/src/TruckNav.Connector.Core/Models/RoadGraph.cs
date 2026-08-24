using TruckNav.Connector.Core.Enums;

namespace TruckNav.Connector.Core.Models;

public sealed record RoadEdge(
    int TargetNodeIndex,
    float DistanceMeters,
    float DurationSeconds,
    RoadTravelDirection ArrivalDirection,
    bool IsOneLaneRoad,
    bool IsFerry,
    int DlcGuard);

public sealed record RoadNode(
    int Index,
    string SourceId,
    WorldPoint Position,
    IReadOnlyList<RoadEdge> ForwardEdges,
    IReadOnlyList<RoadEdge> BackwardEdges);

public sealed record RoadDestination(
    string CityToken,
    string CompanyToken,
    string? CityName,
    string? CompanyName,
    int NodeIndex,
    WorldPoint Position);

public sealed record RoadPointOfInterest(
    NavigationPoiType Type,
    WorldPoint Position);

public sealed record RoadGraphMetadata(
    GameType Game,
    string GameVersion,
    string Generator,
    string GeneratorCommit,
    int FormatVersion,
    DateTimeOffset GeneratedUtc,
    string SourceGamePath,
    string DlcSignature);

public sealed class RoadGraph
{
    public RoadGraph(
        RoadGraphMetadata metadata,
        IReadOnlyList<RoadNode> nodes,
        IReadOnlyList<RoadDestination> destinations,
        IReadOnlyList<RoadPointOfInterest>? pointsOfInterest = null)
    {
        Metadata = metadata;
        Nodes = nodes;
        Destinations = destinations;
        PointsOfInterest = pointsOfInterest ?? [];
    }

    public RoadGraphMetadata Metadata { get; }

    public IReadOnlyList<RoadNode> Nodes { get; }

    public IReadOnlyList<RoadDestination> Destinations { get; }

    public IReadOnlyList<RoadPointOfInterest> PointsOfInterest { get; }
}
