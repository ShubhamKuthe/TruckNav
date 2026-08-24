using TruckNav.Connector.Core.Enums;
using TruckNav.Connector.Core.Models;

namespace TruckNav.Connector.Core.Interfaces;

public interface INavigationStateStore
{
    NavigationSnapshot Snapshot { get; }

    event EventHandler<NavigationSnapshot>? SnapshotChanged;

    void UpdateTelemetry(TelemetrySnapshot telemetry);

    void UpdateStatus(NavigationRuntimeStatus status, string message);

    void UpdateRoute(RoutePlan? route, NavigationRuntimeStatus status, string message);

    void UpdateProgress(RouteProgressSnapshot progress, NavigationRuntimeStatus status, string message);
}
