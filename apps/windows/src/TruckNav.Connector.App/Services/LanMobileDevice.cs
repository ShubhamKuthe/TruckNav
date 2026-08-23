namespace TruckNav.Connector.App.Services;

public sealed record LanMobileDevice(
    string DeviceId,
    string DeviceName,
    string Platform,
    DateTimeOffset LastSeenUtc);
