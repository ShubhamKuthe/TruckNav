namespace TruckNav.Connector.Capture;

public interface IProcessSnapshotProvider
{
    Task<IReadOnlyList<ProcessSnapshot>> GetProcessesAsync(
        CancellationToken cancellationToken = default);
}
