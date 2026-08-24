using System.Collections.Concurrent;
using TruckNav.Connector.Core.Enums;
using TruckNav.Connector.Core.Interfaces;
using TruckNav.Connector.Core.Models;

namespace TruckNav.Connector.Navigation.Data;

public sealed class RoadGraphProvider : IRoadGraphProvider
{
    private readonly IRoadGraphRepository _repository;
    private readonly ConcurrentDictionary<GameType, Lazy<Task<RoadGraph?>>> _graphs = new();

    public RoadGraphProvider(IRoadGraphRepository repository)
    {
        _repository = repository;
    }

    public Task<RoadGraph?> GetGraphAsync(GameType game, CancellationToken cancellationToken = default) =>
        _graphs.GetOrAdd(
            game,
            key => new Lazy<Task<RoadGraph?>>(
                () => _repository.LoadAsync(key, cancellationToken),
                LazyThreadSafetyMode.ExecutionAndPublication)).Value;

    public void Invalidate(GameType game) => _graphs.TryRemove(game, out _);
}
