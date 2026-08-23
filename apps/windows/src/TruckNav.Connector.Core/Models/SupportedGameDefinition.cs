using TruckNav.Connector.Core.Enums;

namespace TruckNav.Connector.Core.Models;

public sealed record SupportedGameDefinition(
    GameType GameType,
    string DisplayName,
    string ProcessName,
    string ProtocolCode);
