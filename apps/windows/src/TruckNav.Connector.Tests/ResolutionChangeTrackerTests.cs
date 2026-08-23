using TruckNav.Connector.Core.Utilities;

namespace TruckNav.Connector.Tests;

public sealed class ResolutionChangeTrackerTests
{
    [Fact]
    public void Observe_ShouldIgnoreTransientResolutionChange()
    {
        var tracker = new ResolutionChangeTracker(3, TimeSpan.FromMilliseconds(750));
        var now = DateTimeOffset.UtcNow;

        Assert.False(tracker.Observe(1920, 1080, 1280, 720, now));
        Assert.False(tracker.Observe(1920, 1080, 1920, 1080, now.AddMilliseconds(100)));
    }

    [Fact]
    public void Observe_ShouldReportStableMaterialResolutionChange()
    {
        var tracker = new ResolutionChangeTracker(3, TimeSpan.FromMilliseconds(750));
        var now = DateTimeOffset.UtcNow;

        Assert.False(tracker.Observe(1920, 1080, 2560, 1440, now));
        Assert.False(tracker.Observe(1920, 1080, 2560, 1440, now.AddMilliseconds(400)));
        Assert.True(tracker.Observe(1920, 1080, 2560, 1440, now.AddMilliseconds(800)));
    }
}
