using TruckNav.Connector.Core.Enums;

namespace TruckNav.Connector.Core.Models;

public sealed record TelemetrySnapshot(
    GameType Game,
    bool Connected,
    bool Paused,
    WorldPoint Position,
    double Elevation,
    double HeadingDegrees,
    double SpeedKph,
    double SpeedLimitKph,
    bool OnJob,
    string? SourceCityId,
    string? SourceCityName,
    string? SourceCompanyId,
    string? SourceCompanyName,
    string? DestinationCityId,
    string? DestinationCityName,
    string? DestinationCompanyId,
    string? DestinationCompanyName,
    double GameNavigationDistanceMeters,
    double GameNavigationTimeSeconds,
    ulong GameTimestamp,
    DateTimeOffset TimestampUtc,
    uint PluginRevision,
    string? GameVersion)
{
    public static TelemetrySnapshot Disconnected { get; } = new(
        GameType.None,
        Connected: false,
        Paused: false,
        default,
        Elevation: 0,
        HeadingDegrees: 0,
        SpeedKph: 0,
        SpeedLimitKph: 0,
        OnJob: false,
        SourceCityId: null,
        SourceCityName: null,
        SourceCompanyId: null,
        SourceCompanyName: null,
        DestinationCityId: null,
        DestinationCityName: null,
        DestinationCompanyId: null,
        DestinationCompanyName: null,
        GameNavigationDistanceMeters: 0,
        GameNavigationTimeSeconds: 0,
        GameTimestamp: 0,
        TimestampUtc: DateTimeOffset.MinValue,
        PluginRevision: 0,
        GameVersion: null);

    public string DestinationKey =>
        $"{Game}:{DestinationCityId ?? DestinationCityName}:{DestinationCompanyId ?? DestinationCompanyName}";

    // The plugin sets OnJob only after trailer attachment, while Freight Market
    // source/destination metadata is already available for the pickup drive.
    public bool HasPickupNavigationTarget =>
        !OnJob &&
        HasValue(SourceCityId, SourceCityName) &&
        HasValue(SourceCompanyId, SourceCompanyName) &&
        HasValue(DestinationCityId, DestinationCityName) &&
        HasValue(DestinationCompanyId, DestinationCompanyName);

    public bool HasNavigationTarget => OnJob || HasPickupNavigationTarget;

    public bool IsNavigatingToPickup => !OnJob && HasPickupNavigationTarget;

    public string NavigationTargetKey => IsNavigatingToPickup
        ? $"{Game}:pickup:{SourceCityId ?? SourceCityName}:{SourceCompanyId ?? SourceCompanyName}"
        : $"{Game}:delivery:{DestinationCityId ?? DestinationCityName}:{DestinationCompanyId ?? DestinationCompanyName}";

    private static bool HasValue(string? id, string? name) =>
        !string.IsNullOrWhiteSpace(id) || !string.IsNullOrWhiteSpace(name);
}
