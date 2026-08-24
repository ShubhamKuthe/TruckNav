using TruckNav.Connector.Core.Enums;

namespace TruckNav.Connector.Core.Models;

public sealed record GameInstallation(
    GameType Game,
    string InstallPath,
    string ExecutablePath,
    string Version,
    string SteamLibraryPath,
    string DlcSignature);

public sealed record TelemetryPluginStatus(
    GameType Game,
    bool GameInstalled,
    bool PluginInstalled,
    bool PluginCurrent,
    string? GamePath,
    string? InstalledPath,
    string Message);

public sealed record NavigationDataStatus(
    GameType Game,
    bool IsReady,
    string RootDirectory,
    string? GameVersion,
    int NodeCount,
    int EdgeCount,
    DateTimeOffset? GeneratedUtc,
    string Message);
