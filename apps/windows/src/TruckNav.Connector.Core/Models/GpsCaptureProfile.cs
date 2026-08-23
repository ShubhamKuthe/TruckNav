using TruckNav.Connector.Core.Enums;

namespace TruckNav.Connector.Core.Models;

public sealed record GpsCaptureProfile(
    int SchemaVersion,
    GameType GameType,
    CaptureSourceType SourceType,
    string SourceId,
    double NormalizedX,
    double NormalizedY,
    double NormalizedWidth,
    double NormalizedHeight,
    int ReferenceSourceWidth,
    int ReferenceSourceHeight,
    DisplayFitMode DisplayFit,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc)
{
    public const int CurrentSchemaVersion = 1;

    public NormalizedRegion Region =>
        new(NormalizedX, NormalizedY, NormalizedWidth, NormalizedHeight);
}
