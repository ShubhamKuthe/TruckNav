namespace TruckNav.Connector.App.Services;

public sealed record ConnectorUiState
{
    public bool HasCompletedOnboarding { get; init; }

    public bool StartWithWindows { get; init; } = true;

    public bool StartMinimized { get; init; } = true;

    public bool MinimizeToTray { get; init; } = true;

    public bool CloseButtonMinimizesToTray { get; init; } = true;

    public bool AllowAutomaticReconnect { get; init; } = true;

    public bool RememberPreviousDevices { get; init; } = true;

    public bool StartNavigationAutomatically { get; init; } = true;

    public bool RouteRenderingAcceleration { get; init; } = true;

    public bool ShowConnectionNotifications { get; init; } = true;

    public bool ShowGameDetectionNotifications { get; init; } = true;

    public bool ShowNavigationNotifications { get; init; }
}
