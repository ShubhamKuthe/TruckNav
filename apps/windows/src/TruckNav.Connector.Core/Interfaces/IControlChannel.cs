namespace TruckNav.Connector.Core.Interfaces;

public interface IControlChannel
{
    Task SendNavigationCommandAsync(Enums.NavigationCommand command, CancellationToken cancellationToken = default);
}
