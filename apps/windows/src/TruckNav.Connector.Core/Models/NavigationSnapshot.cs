using TruckNav.Connector.Core.Enums;

namespace TruckNav.Connector.Core.Models;

public sealed record NavigationSnapshot(
    TelemetrySnapshot Telemetry,
    RoutePlan? Route,
    RouteProgressSnapshot? Progress,
    NavigationRuntimeStatus Status,
    string StatusMessage,
    long RouteRevision,
    DateTimeOffset LastUpdatedUtc)
{
    public static NavigationSnapshot Initial { get; } = new(
        TelemetrySnapshot.Disconnected,
        Route: null,
        Progress: null,
        NavigationRuntimeStatus.NoGame,
        "Start ETS2 or ATS to begin.",
        RouteRevision: 0,
        LastUpdatedUtc: DateTimeOffset.UtcNow);
}
