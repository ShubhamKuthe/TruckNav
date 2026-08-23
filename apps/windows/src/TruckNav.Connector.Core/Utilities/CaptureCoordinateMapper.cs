using TruckNav.Connector.Core.Models;

namespace TruckNav.Connector.Core.Utilities;

public static class CaptureCoordinateMapper
{
    public static double PhysicalPixelsToDips(double physicalPixels, double dpiScale)
    {
        if (dpiScale <= 0 || !double.IsFinite(dpiScale))
        {
            throw new ArgumentOutOfRangeException(nameof(dpiScale));
        }

        return physicalPixels / dpiScale;
    }

    public static double DipsToPhysicalPixels(double dips, double dpiScale)
    {
        if (dpiScale <= 0 || !double.IsFinite(dpiScale))
        {
            throw new ArgumentOutOfRangeException(nameof(dpiScale));
        }

        return dips * dpiScale;
    }

    public static PixelRectangle MapDipRectangleToFrame(
        double x,
        double y,
        double width,
        double height,
        double viewportWidth,
        double viewportHeight,
        int frameWidth,
        int frameHeight)
    {
        if (viewportWidth <= 0 || viewportHeight <= 0 || frameWidth <= 0 || frameHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(viewportWidth));
        }

        var scale = Math.Min(viewportWidth / frameWidth, viewportHeight / frameHeight);
        var renderedWidth = frameWidth * scale;
        var renderedHeight = frameHeight * scale;
        var offsetX = (viewportWidth - renderedWidth) / 2d;
        var offsetY = (viewportHeight - renderedHeight) / 2d;

        return CaptureRegionMath.Clamp(
            new PixelRectangle(
                (int)Math.Round((x - offsetX) / scale),
                (int)Math.Round((y - offsetY) / scale),
                (int)Math.Round(width / scale),
                (int)Math.Round(height / scale)),
            frameWidth,
            frameHeight);
    }
}
