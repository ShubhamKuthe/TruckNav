using TruckNav.Connector.Core.Enums;
using TruckNav.Connector.Core.Models;

namespace TruckNav.Connector.Core.Utilities;

public static class CaptureSelectionMath
{
    public static PixelRectangle Move(
        PixelRectangle selection,
        int deltaX,
        int deltaY,
        int frameWidth,
        int frameHeight)
    {
        return CaptureRegionMath.Clamp(
            selection with
            {
                X = selection.X + deltaX,
                Y = selection.Y + deltaY
            },
            frameWidth,
            frameHeight);
    }

    public static PixelRectangle Resize(
        PixelRectangle selection,
        SelectionHandle handle,
        int deltaX,
        int deltaY,
        int frameWidth,
        int frameHeight,
        int minimumWidth = CaptureRegionMath.MinimumCropWidth,
        int minimumHeight = CaptureRegionMath.MinimumCropHeight)
    {
        if (handle == SelectionHandle.Move)
        {
            return Move(selection, deltaX, deltaY, frameWidth, frameHeight);
        }

        var left = selection.X;
        var top = selection.Y;
        var right = selection.X + selection.Width;
        var bottom = selection.Y + selection.Height;

        if (handle is SelectionHandle.TopLeft or SelectionHandle.BottomLeft)
        {
            left = Math.Clamp(left + deltaX, 0, right - minimumWidth);
        }

        if (handle is SelectionHandle.TopRight or SelectionHandle.BottomRight)
        {
            right = Math.Clamp(right + deltaX, left + minimumWidth, frameWidth);
        }

        if (handle is SelectionHandle.TopLeft or SelectionHandle.TopRight)
        {
            top = Math.Clamp(top + deltaY, 0, bottom - minimumHeight);
        }

        if (handle is SelectionHandle.BottomLeft or SelectionHandle.BottomRight)
        {
            bottom = Math.Clamp(bottom + deltaY, top + minimumHeight, frameHeight);
        }

        return CaptureRegionMath.Clamp(
            new PixelRectangle(left, top, right - left, bottom - top),
            frameWidth,
            frameHeight,
            minimumWidth,
            minimumHeight);
    }
}
