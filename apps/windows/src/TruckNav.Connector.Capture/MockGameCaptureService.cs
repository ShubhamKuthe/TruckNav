using TruckNav.Connector.Core.Enums;
using TruckNav.Connector.Core.Interfaces;
using TruckNav.Connector.Core.Models;

namespace TruckNav.Connector.Capture;

public sealed class MockGameCaptureService : IGameCaptureService
{
    public event EventHandler<VideoFrame>? FrameAvailable;

    public Task<IReadOnlyList<CaptureSource>> GetSourcesAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<CaptureSource> sources =
        [
            new CaptureSource("mock:auto", "Euro Truck Simulator 2 - Auto", CaptureSourceType.DebugMock, 1920, 1080)
        ];

        return Task.FromResult(sources);
    }

    public Task StartAsync(CaptureSource source, CancellationToken cancellationToken = default)
    {
        FrameAvailable?.Invoke(
            this,
            new VideoFrame(
                source.Width,
                source.Height,
                DateTimeOffset.UtcNow,
                Array.Empty<byte>()));

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}
