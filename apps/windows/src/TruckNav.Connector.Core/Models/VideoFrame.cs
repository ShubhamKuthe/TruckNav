namespace TruckNav.Connector.Core.Models;

public sealed record VideoFrame(
    int Width,
    int Height,
    DateTimeOffset CapturedAtUtc,
    byte[] Data);
