namespace TruckNav.Connector.Core.Enums;

public enum StreamState
{
    WaitingForDevice,
    WaitingForGame,
    WaitingForCaptureProfile,
    Starting,
    Streaming,
    Reconnecting,
    CaptureUnavailable,
    StoppedByUser
}
