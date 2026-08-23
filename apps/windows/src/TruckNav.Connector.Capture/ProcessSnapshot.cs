namespace TruckNav.Connector.Capture;

public sealed record ProcessSnapshot(
    int ProcessId,
    string ProcessName,
    nint MainWindowHandle,
    string? MainWindowTitle);
