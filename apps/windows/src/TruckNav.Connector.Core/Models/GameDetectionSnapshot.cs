using TruckNav.Connector.Core.Enums;

namespace TruckNav.Connector.Core.Models;

public sealed record GameDetectionSnapshot(
    GameType GameType,
    int? ProcessId,
    nint WindowHandle,
    string? WindowTitle,
    string? ExecutablePath,
    bool IsRunning);
