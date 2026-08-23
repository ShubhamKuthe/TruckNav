using TruckNav.Connector.Core.Enums;

namespace TruckNav.Connector.Core.Models;

public sealed record RunningGameInfo(
    GameType GameType,
    string ProcessName,
    int ProcessId,
    nint WindowHandle,
    string? WindowTitle,
    bool ProcessRunning,
    bool WindowAvailable);
