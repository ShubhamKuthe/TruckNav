namespace TruckNav.Connector.Networking;

public static class TruckNavNetworkDefaults
{
    public const string AppVersion = "0.1.0";
    public const string ConnectorRole = "connector";
    public const string MobileRole = "mobile";
    public const string WindowsPlatform = "windows";
    public const string AndroidPlatform = "android";
    public const string MdnsServiceType = "_trucknav._tcp.local";
    public const string MdnsRegistrationType = "_trucknav._tcp";
    public const string IntentionalDisconnectReason = "intentional_disconnect";

    public static readonly IReadOnlyList<string> ConnectorCapabilities =
        ["native_navigation", "scs_telemetry", "local_route_graph"];
    public static readonly TimeSpan WebSocketKeepAliveInterval = TimeSpan.FromSeconds(15);
}
