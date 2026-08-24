namespace TruckNav.Connector.Core.Enums;

public enum NavigationRuntimeStatus
{
    NoGame,
    WaitingForTelemetry,
    NavigationDataMissing,
    NoActiveJob,
    ResolvingDestination,
    CalculatingRoute,
    Navigating,
    Rerouting,
    DestinationUnresolved,
    RouteUnavailable,
    Faulted
}
