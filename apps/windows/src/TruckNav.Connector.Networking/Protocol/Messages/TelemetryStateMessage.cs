namespace TruckNav.Connector.Networking.Protocol.Messages;

public sealed record TelemetryStateMessage(
    string Game,
    bool Connected,
    bool Paused,
    double X,
    double Z,
    double Elevation,
    double HeadingDegrees,
    double SpeedKph,
    double SpeedLimitKph,
    bool OnJob,
    string? DestinationCity,
    string? DestinationCompany,
    double GameNavigationDistanceMeters,
    double GameNavigationTimeSeconds,
    string NavigationStatus,
    string StatusMessage,
    DateTimeOffset TimestampUtc)
    : MessageBase(Protocol.ProtocolMessageType.TelemetryState);
