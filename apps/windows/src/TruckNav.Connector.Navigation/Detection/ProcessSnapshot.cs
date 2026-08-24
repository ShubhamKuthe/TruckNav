namespace TruckNav.Connector.Navigation.Detection;

public sealed record ProcessSnapshot(
    int ProcessId,
    string ProcessName,
    nint MainWindowHandle,
    string? MainWindowTitle);

public interface IProcessSnapshotProvider
{
    Task<IReadOnlyList<ProcessSnapshot>> GetProcessesAsync(CancellationToken cancellationToken = default);
}
