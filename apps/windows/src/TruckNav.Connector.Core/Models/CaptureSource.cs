using TruckNav.Connector.Core.Enums;

namespace TruckNav.Connector.Core.Models;

public sealed record CaptureSource(
    string Id,
    string DisplayName,
    CaptureSourceType SourceType,
    int Width,
    int Height);
