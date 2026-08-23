using TruckNav.Connector.Networking.Models;

namespace TruckNav.Connector.Networking.Identity;

public interface IConnectorIdentityProvider
{
    ConnectorPeerInfo GetIdentity();
}
