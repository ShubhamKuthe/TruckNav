using TruckNav.Connector.Core.Enums;

namespace TruckNav.Connector.Core.Utilities;

public static class NavigationCommandCatalog
{
    private static readonly HashSet<NavigationCommand> AllowedCommands =
    [
        NavigationCommand.ZoomIn,
        NavigationCommand.ZoomOut,
        NavigationCommand.CycleNavigationView
    ];

    public static bool IsSupported(NavigationCommand command) => AllowedCommands.Contains(command);
}
