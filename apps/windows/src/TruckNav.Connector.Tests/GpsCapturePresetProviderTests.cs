using TruckNav.Connector.Capture;
using TruckNav.Connector.Core.Enums;

namespace TruckNav.Connector.Tests;

public sealed class GpsCapturePresetProviderTests
{
    private readonly StandardGpsCapturePresetProvider _provider = new();

    [Fact]
    public void GetStandardRouteAdvisorPreset_ShouldReturnKnown16By9Preset()
    {
        var preset = _provider.GetStandardRouteAdvisorPreset(
            GameType.EuroTruckSimulator2,
            1920,
            1080);

        Assert.NotNull(preset);
    }

    [Fact]
    public void GetStandardRouteAdvisorPreset_ShouldNotGuessForUnknownAspectRatio()
    {
        var preset = _provider.GetStandardRouteAdvisorPreset(
            GameType.AmericanTruckSimulator,
            1024,
            768);

        Assert.Null(preset);
    }
}
