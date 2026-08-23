using TruckNav.Connector.Core.Enums;
using TruckNav.Connector.Core.Utilities;

namespace TruckNav.Connector.Tests;

public sealed class NavigationCommandCatalogTests
{
    [Theory]
    [InlineData(NavigationCommand.ZoomIn)]
    [InlineData(NavigationCommand.ZoomOut)]
    [InlineData(NavigationCommand.CycleNavigationView)]
    public void IsSupported_ShouldAcceptKnownCommands(NavigationCommand command)
    {
        Assert.True(NavigationCommandCatalog.IsSupported(command));
    }
}
