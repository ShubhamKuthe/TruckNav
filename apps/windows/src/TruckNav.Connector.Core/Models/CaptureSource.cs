using TruckNav.Connector.Core.Enums;

namespace TruckNav.Connector.Core.Models;

public sealed record CaptureSource(
    string Id,
    string DisplayName,
    CaptureSourceType SourceType,
    int Width,
    int Height,
    nint NativeHandle = 0,
    GameType GameType = GameType.None,
    int Left = 0,
    int Top = 0,
    bool IsPrimary = false);
