namespace TruckNav.Connector.Networking.Protocol;

public static class ProtocolMessageType
{
    public const string Hello = "hello";
    public const string HelloAck = "hello_ack";
    public const string DeviceState = "device_state";
    public const string GameState = "game_state";
    public const string CaptureState = "capture_state";
    public const string StreamState = "stream_state";
    public const string WebRtcOffer = "webrtc_offer";
    public const string WebRtcAnswer = "webrtc_answer";
    public const string WebRtcIce = "webrtc_ice";
    public const string NavigationCommand = "navigation_command";
    public const string Ping = "ping";
    public const string Pong = "pong";
    public const string Error = "error";
}
