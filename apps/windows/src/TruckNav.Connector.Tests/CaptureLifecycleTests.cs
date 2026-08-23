using TruckNav.Connector.Capture;
using TruckNav.Connector.Core.Enums;
using TruckNav.Connector.Core.Models;
using TruckNav.Connector.Core.Utilities;

namespace TruckNav.Connector.Tests;

public sealed class CaptureLifecycleTests
{
    [Fact]
    public async Task MockCapture_ShouldStartProduceFrameAndStopCleanly()
    {
        var service = new MockGameCaptureService();
        var source = CreateSource("mock:a");
        var frameReceived = new TaskCompletionSource<VideoFrame>(TaskCreationOptions.RunContinuationsAsynchronously);
        service.FrameAvailable += (_, frame) => frameReceived.TrySetResult(frame);

        await service.StartAsync(source);
        var frame = await frameReceived.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await service.StopAsync();

        Assert.Equal(320, frame.Width);
        Assert.False(service.CurrentSnapshot.IsRunning);
    }

    [Fact]
    public async Task MockCapture_ShouldReplaceSourceWithoutLeavingOldSessionRunning()
    {
        var service = new MockGameCaptureService();

        await service.StartAsync(CreateSource("mock:a"));
        await service.StartAsync(CreateSource("mock:b"));

        Assert.Equal("mock:b", service.CurrentSnapshot.Source!.Id);
        await service.StopAsync();
    }

    [Fact]
    public async Task Stop_ShouldRepresentGameClosingDuringCapture()
    {
        var service = new MockGameCaptureService();
        await service.StartAsync(CreateSource("mock:game"));

        await service.StopAsync();

        Assert.Equal(CaptureRuntimeSnapshot.Stopped, service.CurrentSnapshot);
    }

    [Fact]
    public void LatestValueBuffer_ShouldRetainOnlyNewestFrame()
    {
        var buffer = new LatestValueBuffer<string>();

        buffer.Publish("frame-1");
        buffer.Publish("frame-2");
        buffer.Publish("frame-3");

        Assert.Equal("frame-3", buffer.Take());
        Assert.Null(buffer.Take());
    }

    private static CaptureSource CreateSource(string id) =>
        new(id, "TRUCKNAV TEST FEED", CaptureSourceType.DebugMock, 320, 180);
}
