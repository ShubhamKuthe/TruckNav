using TruckNav.Connector.Core.Models;

namespace TruckNav.Connector.Core.Interfaces;

public interface IGameCaptureService
{
    CaptureRuntimeSnapshot CurrentSnapshot { get; }

    Task<IReadOnlyList<CaptureSource>> GetSourcesAsync(CancellationToken cancellationToken = default);
    Task StartAsync(CaptureSource source, CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);

    event EventHandler<VideoFrame>? FrameAvailable;
    event EventHandler<CaptureRuntimeSnapshot>? SnapshotChanged;
}
