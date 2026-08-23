using TruckNav.Connector.Core.Models;
using TruckNav.Connector.Core.Utilities;

namespace TruckNav.Connector.Tests;

public sealed class CaptureRegionMathTests
{
    [Fact]
    public void Normalize_ShouldCreateExpectedFractions()
    {
        var region = CaptureRegionMath.Normalize(100, 50, 400, 200, 1000, 500);

        Assert.Equal(0.1d, region.X, 5);
        Assert.Equal(0.1d, region.Y, 5);
        Assert.Equal(0.4d, region.Width, 5);
        Assert.Equal(0.4d, region.Height, 5);
    }

    [Fact]
    public void Project_ShouldRespectSourceBounds()
    {
        var region = new NormalizedRegion(0.8d, 0.8d, 0.2d, 0.2d);

        var projected = CaptureRegionMath.Project(region, 1000, 500);

        Assert.Equal(800, projected.X);
        Assert.Equal(400, projected.Y);
        Assert.Equal(200, projected.Width);
        Assert.Equal(100, projected.Height);
    }

    [Theory]
    [InlineData(1920, 1080, 1920, 1080, false)]
    [InlineData(1920, 1080, 1800, 1080, true)]
    [InlineData(1920, 1080, 1920, 1030, false)]
    public void HasMaterialResolutionChange_ShouldUseFivePercentThreshold(
        int referenceWidth,
        int referenceHeight,
        int currentWidth,
        int currentHeight,
        bool expected)
    {
        var result = CaptureRegionMath.HasMaterialResolutionChange(referenceWidth, referenceHeight, currentWidth, currentHeight);

        Assert.Equal(expected, result);
    }
}
