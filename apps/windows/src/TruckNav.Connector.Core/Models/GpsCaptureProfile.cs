using TruckNav.Connector.Core.Enums;

namespace TruckNav.Connector.Core.Models;

public sealed record GpsCaptureProfile(
    GameType GameType,
    CaptureSourceType SourceType,
    string SourceId,
    int SourceWidth,
    int SourceHeight,
    double NormalizedX,
    double NormalizedY,
    double NormalizedWidth,
    double NormalizedHeight,
    int ReferenceWidth,
    int ReferenceHeight,
    DisplayOrientation DisplayFit,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
