using TruckNav.Connector.Core.Enums;
using TruckNav.Connector.Core.Interfaces;
using TruckNav.Connector.Core.Models;

namespace TruckNav.Connector.Navigation.State;

public sealed class NavigationStateStore : INavigationStateStore
{
    private readonly object _gate = new();
    private NavigationSnapshot _snapshot = NavigationSnapshot.Initial;

    public NavigationSnapshot Snapshot
    {
        get
        {
            lock (_gate)
            {
                return _snapshot;
            }
        }
    }

    public event EventHandler<NavigationSnapshot>? SnapshotChanged;

    public void UpdateTelemetry(TelemetrySnapshot telemetry)
    {
        NavigationSnapshot snapshot;
        lock (_gate)
        {
            _snapshot = _snapshot with
            {
                Telemetry = telemetry,
                LastUpdatedUtc = DateTimeOffset.UtcNow
            };
            snapshot = _snapshot;
        }

        SnapshotChanged?.Invoke(this, snapshot);
    }

    public void UpdateStatus(NavigationRuntimeStatus status, string message)
    {
        NavigationSnapshot? snapshot = null;
        lock (_gate)
        {
            if (_snapshot.Status == status && string.Equals(_snapshot.StatusMessage, message, StringComparison.Ordinal))
            {
                return;
            }

            _snapshot = _snapshot with
            {
                Status = status,
                StatusMessage = message,
                LastUpdatedUtc = DateTimeOffset.UtcNow
            };
            snapshot = _snapshot;
        }

        SnapshotChanged?.Invoke(this, snapshot);
    }

    public void UpdateRoute(RoutePlan? route, NavigationRuntimeStatus status, string message)
    {
        NavigationSnapshot snapshot;
        lock (_gate)
        {
            _snapshot = _snapshot with
            {
                Route = route,
                Progress = null,
                Status = status,
                StatusMessage = message,
                RouteRevision = _snapshot.RouteRevision + 1,
                LastUpdatedUtc = DateTimeOffset.UtcNow
            };
            snapshot = _snapshot;
        }

        SnapshotChanged?.Invoke(this, snapshot);
    }

    public void UpdateProgress(RouteProgressSnapshot progress, NavigationRuntimeStatus status, string message)
    {
        NavigationSnapshot snapshot;
        lock (_gate)
        {
            _snapshot = _snapshot with
            {
                Progress = progress,
                Status = status,
                StatusMessage = message,
                LastUpdatedUtc = DateTimeOffset.UtcNow
            };
            snapshot = _snapshot;
        }

        SnapshotChanged?.Invoke(this, snapshot);
    }
}
