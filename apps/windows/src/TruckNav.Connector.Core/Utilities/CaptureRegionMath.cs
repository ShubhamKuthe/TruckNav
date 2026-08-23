using TruckNav.Connector.Core.Models;

namespace TruckNav.Connector.Core.Utilities;

public static class CaptureRegionMath
{
    public static NormalizedRegion Normalize(int x, int y, int width, int height, int sourceWidth, int sourceHeight)
    {
        if (sourceWidth <= 0 || sourceHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sourceWidth), "Source dimensions must be positive.");
        }

        if (width <= 0 || height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Selection dimensions must be positive.");
        }

        if (x < 0 || y < 0 || x + width > sourceWidth || y + height > sourceHeight)
        {
            throw new ArgumentOutOfRangeException(nameof(x), "Selection must stay within source bounds.");
        }

        return new NormalizedRegion(
            X: (double)x / sourceWidth,
            Y: (double)y / sourceHeight,
            Width: (double)width / sourceWidth,
            Height: (double)height / sourceHeight);
    }

    public static PixelRectangle Project(NormalizedRegion region, int sourceWidth, int sourceHeight)
    {
        if (sourceWidth <= 0 || sourceHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sourceWidth), "Source dimensions must be positive.");
        }

        ValidateNormalizedRegion(region);

        var x = (int)Math.Round(region.X * sourceWidth, MidpointRounding.AwayFromZero);
        var y = (int)Math.Round(region.Y * sourceHeight, MidpointRounding.AwayFromZero);
        var width = (int)Math.Round(region.Width * sourceWidth, MidpointRounding.AwayFromZero);
        var height = (int)Math.Round(region.Height * sourceHeight, MidpointRounding.AwayFromZero);

        width = Math.Max(1, width);
        height = Math.Max(1, height);

        if (x + width > sourceWidth)
        {
            width = sourceWidth - x;
        }

        if (y + height > sourceHeight)
        {
            height = sourceHeight - y;
        }

        return new PixelRectangle(x, y, width, height);
    }

    public static bool HasMaterialResolutionChange(
        int referenceWidth,
        int referenceHeight,
        int currentWidth,
        int currentHeight)
    {
        if (referenceWidth <= 0 || referenceHeight <= 0 || currentWidth <= 0 || currentHeight <= 0)
        {
            return true;
        }

        var widthDelta = Math.Abs(referenceWidth - currentWidth) / (double)referenceWidth;
        var heightDelta = Math.Abs(referenceHeight - currentHeight) / (double)referenceHeight;

        return widthDelta > 0.05 || heightDelta > 0.05;
    }

    private static void ValidateNormalizedRegion(NormalizedRegion region)
    {
        if (region.X < 0 || region.Y < 0 || region.Width <= 0 || region.Height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(region), "Normalized region values must be positive and in range.");
        }

        if (region.X + region.Width > 1 || region.Y + region.Height > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(region), "Normalized region must remain within the source bounds.");
        }
    }
}
