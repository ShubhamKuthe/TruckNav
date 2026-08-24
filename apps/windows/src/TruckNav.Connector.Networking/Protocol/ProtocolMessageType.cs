namespace TruckNav.Connector.Networking.Protocol;

public static class ProtocolMessageType
{
    public const string Hello = "hello";
    public const string HelloAck = "hello_ack";
    public const string DeviceState = "device_state";
    public const string GameState = "game_state";
    public const string GameStateRequest = "game_state_request";
    public const string TelemetryState = "telemetry_state";
    public const string RouteState = "route_state";
    public const string RouteProgress = "route_progress";
    public const string NavigationCommand = "navigation_command";
    public const string Ping = "ping";
    public const string Pong = "pong";
    public const string Error = "error";
}
