using TruckNav.Connector.Core.Enums;

namespace TruckNav.Connector.Core.Models;

public sealed record CaptureRuntimeSnapshot(
    bool IsRunning,
    CaptureSource? Source,
    int FrameWidth,
    int FrameHeight,
    double PreviewFramesPerSecond,
    CaptureErrorCode ErrorCode,
    string? TechnicalDetail)
{
    public static CaptureRuntimeSnapshot Stopped { get; } =
        new(false, null, 0, 0, 0, CaptureErrorCode.None, null);
}
