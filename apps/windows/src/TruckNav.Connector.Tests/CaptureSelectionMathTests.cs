using TruckNav.Connector.Core.Enums;
using TruckNav.Connector.Core.Models;
using TruckNav.Connector.Core.Utilities;

namespace TruckNav.Connector.Tests;

public sealed class CaptureSelectionMathTests
{
    [Fact]
    public void Move_ShouldNeverLeaveFrameBounds()
    {
        var selection = new PixelRectangle(100, 100, 300, 200);

        var moved = CaptureSelectionMath.Move(selection, 5000, -5000, 1920, 1080);

        Assert.Equal(new PixelRectangle(1620, 0, 300, 200), moved);
    }

    [Theory]
    [InlineData(SelectionHandle.TopLeft, -500, -500)]
    [InlineData(SelectionHandle.TopRight, 5000, -500)]
    [InlineData(SelectionHandle.BottomLeft, -500, 5000)]
    [InlineData(SelectionHandle.BottomRight, 5000, 5000)]
    public void Resize_ShouldStayInsideFrame(SelectionHandle handle, int deltaX, int deltaY)
    {
        var resized = CaptureSelectionMath.Resize(
            new PixelRectangle(200, 200, 300, 200),
            handle,
            deltaX,
            deltaY,
            1000,
            600);

        Assert.InRange(resized.X, 0, 1000 - resized.Width);
        Assert.InRange(resized.Y, 0, 600 - resized.Height);
        Assert.InRange(resized.Width, 64, 1000);
        Assert.InRange(resized.Height, 64, 600);
    }
}
