namespace TruckNav.Connector.Networking.Protocol;

public static class ProtocolCompatibility
{
    public const int CurrentVersion = 1;

    public static bool IsCompatible(int remoteVersion) => remoteVersion == CurrentVersion;

    public static string GetMismatchMessage(int remoteVersion) =>
        $"Protocol version mismatch. Local={CurrentVersion}, Remote={remoteVersion}.";
}
