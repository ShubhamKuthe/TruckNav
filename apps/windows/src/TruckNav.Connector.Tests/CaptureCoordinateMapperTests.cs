using TruckNav.Connector.Core.Utilities;

namespace TruckNav.Connector.Tests;

public sealed class CaptureCoordinateMapperTests
{
    [Theory]
    [InlineData(1.0, 100, 100)]
    [InlineData(1.25, 100, 125)]
    [InlineData(1.5, 100, 150)]
    public void DipsToPhysicalPixels_ShouldRespectDpiScale(double scale, double dips, double expectedPixels)
    {
        Assert.Equal(expectedPixels, CaptureCoordinateMapper.DipsToPhysicalPixels(dips, scale), 5);
        Assert.Equal(dips, CaptureCoordinateMapper.PhysicalPixelsToDips(expectedPixels, scale), 5);
    }

    [Fact]
    public void MapDipRectangleToFrame_ShouldIgnoreLetterboxing()
    {
        var mapped = CaptureCoordinateMapper.MapDipRectangleToFrame(
            x: 400,
            y: 250,
            width: 200,
            height: 100,
            viewportWidth: 1000,
            viewportHeight: 700,
            frameWidth: 1920,
            frameHeight: 1080);

        Assert.Equal(768, mapped.X);
        Assert.Equal(348, mapped.Y);
        Assert.Equal(384, mapped.Width);
        Assert.Equal(192, mapped.Height);
    }
}
