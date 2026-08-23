using TruckNav.Connector.Networking.Models;

namespace TruckNav.Connector.Networking.Session;

public sealed record ConnectorSessionReservation(
    string ReservationId,
    ConnectorPeerInfo RemotePeer);
