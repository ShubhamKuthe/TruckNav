using TruckNav.Connector.Core.Enums;

namespace TruckNav.Connector.Core.Models;

// Frame data is an owned, tightly packed managed snapshot. Native capture surfaces
// are disposed by the provider before this object crosses the capture boundary.
public sealed record VideoFrame(
    int Width,
    int Height,
    int Stride,
    CapturePixelFormat PixelFormat,
    DateTimeOffset CapturedAtUtc,
    long SequenceNumber,
    byte[] Data);
