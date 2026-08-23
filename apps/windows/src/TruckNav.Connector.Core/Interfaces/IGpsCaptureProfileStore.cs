using TruckNav.Connector.Core.Enums;
using TruckNav.Connector.Core.Models;

namespace TruckNav.Connector.Core.Interfaces;

public interface IGpsCaptureProfileStore
{
    event EventHandler? ProfilesChanged;

    IReadOnlyDictionary<GameType, GpsCaptureProfile> GetAll();
    GpsCaptureProfile? Get(GameType gameType);
    void Save(GpsCaptureProfile profile);
}
