using System.Diagnostics;

namespace TruckNav.Connector.Capture;

public sealed class SystemProcessSnapshotProvider : IProcessSnapshotProvider
{
    public Task<IReadOnlyList<ProcessSnapshot>> GetProcessesAsync(
        CancellationToken cancellationToken = default)
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

                    nint mainWindowHandle = 0;
                    string? mainWindowTitle = null;

                    try
                    {
                        mainWindowHandle = process.MainWindowHandle;
                    }
                    catch
                    {
                    }

                    if (mainWindowHandle != 0)
                    {
                        try
                        {
                            mainWindowTitle = process.MainWindowTitle;
                        }
                        catch
                        {
                        }
                    }

                    snapshots.Add(new ProcessSnapshot(
                        process.Id,
                        processName,
                        mainWindowHandle,
                        string.IsNullOrWhiteSpace(mainWindowTitle)
                            ? null
                            : mainWindowTitle));
                }
                catch
                {
                }
            }
        }

        return Task.FromResult<IReadOnlyList<ProcessSnapshot>>(snapshots);
    }
}
