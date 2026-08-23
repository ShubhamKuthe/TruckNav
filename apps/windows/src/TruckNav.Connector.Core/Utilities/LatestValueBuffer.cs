namespace TruckNav.Connector.Core.Utilities;

public sealed class LatestValueBuffer<T> where T : class
{
    private T? _latest;

    public bool HasValue => Volatile.Read(ref _latest) is not null;

    public void Publish(T value) => Interlocked.Exchange(ref _latest, value);

    public T? Take() => Interlocked.Exchange(ref _latest, null);
}
