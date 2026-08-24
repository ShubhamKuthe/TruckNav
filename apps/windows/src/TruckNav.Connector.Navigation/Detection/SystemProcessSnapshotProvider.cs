using System.Diagnostics;

namespace TruckNav.Connector.Navigation.Detection;

public sealed class SystemProcessSnapshotProvider : IProcessSnapshotProvider
{
    public Task<IReadOnlyList<ProcessSnapshot>> GetProcessesAsync(CancellationToken cancellationToken = default)
    {
        var snapshots = new List<ProcessSnapshot>();
        foreach (var process in Process.GetProcesses())
        {
            cancellationToken.ThrowIfCancellationRequested();
            using (process)
            {
                try
                {
                    var processName = process.ProcessName;
                    if (string.IsNullOrWhiteSpace(processName))
                    {
                        continue;
                    }

                    nint windowHandle = 0;
                    string? windowTitle = null;
                    try
                    {
                        windowHandle = process.MainWindowHandle;
                        windowTitle = windowHandle == 0 ? null : process.MainWindowTitle;
                    }
                    catch
                    {
                    }

                    snapshots.Add(new ProcessSnapshot(
                        process.Id,
                        processName,
                        windowHandle,
                        string.IsNullOrWhiteSpace(windowTitle) ? null : windowTitle));
                }
                catch
                {
                }
            }
        }

        return Task.FromResult<IReadOnlyList<ProcessSnapshot>>(snapshots);
    }
}
