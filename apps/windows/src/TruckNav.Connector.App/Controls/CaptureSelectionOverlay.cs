using System.Windows.Input;
using System.Windows.Media;
using TruckNav.Connector.Core.Enums;
using TruckNav.Connector.Core.Models;
using TruckNav.Connector.Core.Utilities;

namespace TruckNav.Connector.App.Controls;

public sealed class CaptureSelectionOverlay : FrameworkElement
{
    public static readonly DependencyProperty FrameWidthProperty = DependencyProperty.Register(
        nameof(FrameWidth), typeof(int), typeof(CaptureSelectionOverlay),
        new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty FrameHeightProperty = DependencyProperty.Register(
        nameof(FrameHeight), typeof(int), typeof(CaptureSelectionOverlay),
        new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty SelectionProperty = DependencyProperty.Register(
        nameof(Selection), typeof(PixelRectangle), typeof(CaptureSelectionOverlay),
        new FrameworkPropertyMetadata(
            null,
            FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    private static readonly Brush ShadeBrush = new SolidColorBrush(Color.FromArgb(150, 6, 8, 7));
    private static readonly Brush AccentBrush = new SolidColorBrush(Color.FromRgb(245, 158, 50));
    private static readonly Pen AccentPen = new(AccentBrush, 2);
    private const double HandleRadius = 6;
    private const double HitRadius = 12;

    private Point _dragOrigin;
    private PixelRectangle? _dragStartSelection;
    private SelectionHandle _activeHandle;

    public int FrameWidth
    {
        get => (int)GetValue(FrameWidthProperty);
        set => SetValue(FrameWidthProperty, value);
    }

    public int FrameHeight
    {
        get => (int)GetValue(FrameHeightProperty);
        set => SetValue(FrameHeightProperty, value);
    }

    public PixelRectangle? Selection
    {
        get => (PixelRectangle?)GetValue(SelectionProperty);
        set => SetValue(SelectionProperty, value);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);
        if (!TryGetGeometry(out var imageBounds, out var selectionBounds))
        {
            return;
        }

        drawingContext.DrawRectangle(ShadeBrush, null, new Rect(imageBounds.Left, imageBounds.Top, imageBounds.Width, selectionBounds.Top - imageBounds.Top));
        drawingContext.DrawRectangle(ShadeBrush, null, new Rect(imageBounds.Left, selectionBounds.Bottom, imageBounds.Width, imageBounds.Bottom - selectionBounds.Bottom));
        drawingContext.DrawRectangle(ShadeBrush, null, new Rect(imageBounds.Left, selectionBounds.Top, selectionBounds.Left - imageBounds.Left, selectionBounds.Height));
        drawingContext.DrawRectangle(ShadeBrush, null, new Rect(selectionBounds.Right, selectionBounds.Top, imageBounds.Right - selectionBounds.Right, selectionBounds.Height));
        drawingContext.DrawRoundedRectangle(null, AccentPen, selectionBounds, 7, 7);

        foreach (var point in GetCornerPoints(selectionBounds))
        {
            drawingContext.DrawEllipse(AccentBrush, null, point, HandleRadius, HandleRadius);
        }
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        if (!TryGetGeometry(out _, out var selectionBounds) || Selection is null)
        {
            return;
        }

        var pointer = e.GetPosition(this);
        _activeHandle = HitTestHandle(pointer, selectionBounds);
        if (_activeHandle == SelectionHandle.None)
        {
            return;
        }

        _dragOrigin = pointer;
        _dragStartSelection = Selection;
        CaptureMouse();
        e.Handled = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (!IsMouseCaptured || _dragStartSelection is null || FrameWidth <= 0 || FrameHeight <= 0)
        {
            return;
        }

        var imageBounds = GetImageBounds();
        var scale = imageBounds.Width / FrameWidth;
        if (scale <= 0)
        {
            return;
        }

        var pointer = e.GetPosition(this);
        var deltaX = (int)Math.Round((pointer.X - _dragOrigin.X) / scale);
        var deltaY = (int)Math.Round((pointer.Y - _dragOrigin.Y) / scale);
        Selection = CaptureSelectionMath.Resize(
            _dragStartSelection,
            _activeHandle,
            deltaX,
            deltaY,
            FrameWidth,
            FrameHeight);
        e.Handled = true;
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        if (IsMouseCaptured)
        {
            ReleaseMouseCapture();
            _activeHandle = SelectionHandle.None;
            _dragStartSelection = null;
            e.Handled = true;
        }
    }

    private bool TryGetGeometry(out Rect imageBounds, out Rect selectionBounds)
    {
        imageBounds = GetImageBounds();
        selectionBounds = Rect.Empty;
        if (Selection is null || FrameWidth <= 0 || FrameHeight <= 0 || imageBounds.IsEmpty)
        {
            return false;
        }

        var scale = imageBounds.Width / FrameWidth;
        selectionBounds = new Rect(
            imageBounds.Left + Selection.X * scale,
            imageBounds.Top + Selection.Y * scale,
            Selection.Width * scale,
            Selection.Height * scale);
        return true;
    }

    private Rect GetImageBounds()
    {
        if (FrameWidth <= 0 || FrameHeight <= 0 || ActualWidth <= 0 || ActualHeight <= 0)
        {
            return Rect.Empty;
        }

        var scale = Math.Min(ActualWidth / FrameWidth, ActualHeight / FrameHeight);
        var width = FrameWidth * scale;
        var height = FrameHeight * scale;
        return new Rect((ActualWidth - width) / 2, (ActualHeight - height) / 2, width, height);
    }

    private static SelectionHandle HitTestHandle(Point point, Rect selection)
    {
        var corners = GetCornerPoints(selection).ToArray();
        var handles = new[]
        {
            SelectionHandle.TopLeft,
            SelectionHandle.TopRight,
            SelectionHandle.BottomLeft,
            SelectionHandle.BottomRight
        };

        for (var index = 0; index < corners.Length; index++)
        {
            if ((point - corners[index]).Length <= HitRadius)
            {
                return handles[index];
            }
        }

        return selection.Contains(point) ? SelectionHandle.Move : SelectionHandle.None;
    }

    private static IEnumerable<Point> GetCornerPoints(Rect rectangle)
    {
        yield return rectangle.TopLeft;
        yield return rectangle.TopRight;
        yield return rectangle.BottomLeft;
        yield return rectangle.BottomRight;
    }
}
