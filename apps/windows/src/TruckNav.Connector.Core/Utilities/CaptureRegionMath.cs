using TruckNav.Connector.Core.Models;

namespace TruckNav.Connector.Core.Utilities;

public static class CaptureRegionMath
{
    public const int MinimumCropWidth = 64;
    public const int MinimumCropHeight = 64;
    private const double ValidationTolerance = 0.000001d;

    public static NormalizedRegion Normalize(int x, int y, int width, int height, int sourceWidth, int sourceHeight)
    {
        if (sourceWidth <= 0 || sourceHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sourceWidth), "Source dimensions must be positive.");
        }

        if (width < MinimumCropWidth || height < MinimumCropHeight)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Selection is smaller than the minimum crop size.");
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

    public static PixelRectangle Clamp(
        PixelRectangle rectangle,
        int sourceWidth,
        int sourceHeight,
        int minimumWidth = MinimumCropWidth,
        int minimumHeight = MinimumCropHeight)
    {
        if (sourceWidth <= 0 || sourceHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sourceWidth));
        }

        var minWidth = Math.Min(Math.Max(1, minimumWidth), sourceWidth);
        var minHeight = Math.Min(Math.Max(1, minimumHeight), sourceHeight);
        var width = Math.Clamp(rectangle.Width, minWidth, sourceWidth);
        var height = Math.Clamp(rectangle.Height, minHeight, sourceHeight);
        var x = Math.Clamp(rectangle.X, 0, sourceWidth - width);
        var y = Math.Clamp(rectangle.Y, 0, sourceHeight - height);

        return new PixelRectangle(x, y, width, height);
    }

    public static PixelRectangle CreateDefaultSelection(int sourceWidth, int sourceHeight)
    {
        if (sourceWidth <= 0 || sourceHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sourceWidth));
        }

        var width = Math.Max(MinimumCropWidth, (int)Math.Round(sourceWidth * 0.30d));
        var height = Math.Max(MinimumCropHeight, (int)Math.Round(sourceHeight * 0.38d));
        width = Math.Min(width, sourceWidth);
        height = Math.Min(height, sourceHeight);

        return new PixelRectangle(
            Math.Max(0, sourceWidth - width - (int)Math.Round(sourceWidth * 0.025d)),
            Math.Max(0, sourceHeight - height - (int)Math.Round(sourceHeight * 0.035d)),
            width,
            height);
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

    public static bool IsValid(NormalizedRegion region)
    {
        return double.IsFinite(region.X) &&
               double.IsFinite(region.Y) &&
               double.IsFinite(region.Width) &&
               double.IsFinite(region.Height) &&
               region.X >= 0 &&
               region.Y >= 0 &&
               region.Width > 0 &&
               region.Height > 0 &&
               region.X + region.Width <= 1 + ValidationTolerance &&
               region.Y + region.Height <= 1 + ValidationTolerance;
    }

    public static void ValidateNormalizedRegion(NormalizedRegion region)
    {
        if (!IsValid(region))
        {
            throw new ArgumentOutOfRangeException(nameof(region), "Normalized region must remain within the source bounds.");
        }
    }
}
