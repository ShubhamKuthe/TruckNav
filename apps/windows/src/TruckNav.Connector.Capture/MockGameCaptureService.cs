using TruckNav.Connector.Core.Enums;
using TruckNav.Connector.Core.Interfaces;
using TruckNav.Connector.Core.Models;

namespace TruckNav.Connector.Capture;

public sealed class MockGameCaptureService : IGameCaptureService
{
    private CancellationTokenSource? _captureCancellation;
    private Task? _captureTask;

    public CaptureRuntimeSnapshot CurrentSnapshot { get; private set; } = CaptureRuntimeSnapshot.Stopped;

    public event EventHandler<VideoFrame>? FrameAvailable;
    public event EventHandler<CaptureRuntimeSnapshot>? SnapshotChanged;

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
        _captureCancellation?.Cancel();
        _captureCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        CurrentSnapshot = new CaptureRuntimeSnapshot(
            true, source, source.Width, source.Height, 20, CaptureErrorCode.None, "DEVELOPMENT / TEST ONLY");
        SnapshotChanged?.Invoke(this, CurrentSnapshot);
        _captureTask = RunTestPatternAsync(source, _captureCancellation.Token);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        _captureCancellation?.Cancel();
        if (_captureTask is not null)
        {
            try
            {
                await _captureTask.WaitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
            }
        }

        CurrentSnapshot = CaptureRuntimeSnapshot.Stopped;
        SnapshotChanged?.Invoke(this, CurrentSnapshot);
    }

    private async Task RunTestPatternAsync(CaptureSource source, CancellationToken cancellationToken)
    {
        var sequence = 0L;
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(50));

        while (await timer.WaitForNextTickAsync(cancellationToken))
        {
            sequence++;
            var width = source.Width;
            var height = source.Height;
            var stride = width * 4;
            var data = new byte[stride * height];
            var movingX = (int)(sequence * 12 % Math.Max(1, width - 180));

            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var index = y * stride + x * 4;
                    var inside = x >= movingX && x < movingX + 180 && y >= 80 && y < 200;
                    data[index] = inside ? (byte)40 : (byte)18;
                    data[index + 1] = inside ? (byte)158 : (byte)24;
                    data[index + 2] = inside ? (byte)245 : (byte)29;
                    data[index + 3] = 255;
                }
            }

            FrameAvailable?.Invoke(
                this,
                new VideoFrame(
                    width,
                    height,
                    stride,
                    CapturePixelFormat.Bgra8Premultiplied,
                    DateTimeOffset.UtcNow,
                    sequence,
                    data));
        }
    }
}
