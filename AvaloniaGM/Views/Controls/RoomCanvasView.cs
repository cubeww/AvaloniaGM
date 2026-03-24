using System;
using System.ComponentModel;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using AvaloniaGM.Models;
using AvaloniaGM.ViewModels;

namespace AvaloniaGM.Views.Controls;

public class RoomCanvasView : Control
{
    private static readonly IBrush CanvasBackgroundBrush = new SolidColorBrush(Color.Parse("#FF1F1F1F"));
    private static readonly IBrush RoomOutlineBrush = new SolidColorBrush(Color.Parse("#FF808080"));
    private static readonly IBrush GridBrush = new SolidColorBrush(Color.Parse("#2FFFFFFF"));
    private static readonly IBrush PlaceholderFillBrush = new SolidColorBrush(Color.Parse("#5589A7D6"));
    private static readonly IBrush HoverOutlineBrush = new SolidColorBrush(Color.Parse("#CC5FA8FF"));
    private static readonly IBrush HoverFillBrush = new SolidColorBrush(Color.Parse("#335FA8FF"));
    private static readonly IBrush EraseOutlineBrush = new SolidColorBrush(Color.Parse("#CCD65F5F"));
    private static readonly Pen RoomOutlinePen = new(new SolidColorBrush(Color.Parse("#FF808080")), 1);
    private static readonly Pen GridPen = new(GridBrush, 1);
    private static readonly Pen HoverPen = new(HoverOutlineBrush, 1);
    private static readonly Pen ErasePen = new(EraseOutlineBrush, 1);
    private static readonly Pen PlaceholderPen = new(new SolidColorBrush(Color.Parse("#FF9DBBE8")), 1);

    private RoomEditorViewModel? _viewModel;
    private Point? _hoverPoint;
    private bool _isPrimaryDragging;
    private bool _isSecondaryDragging;
    private bool _isMiddleDragging;
    private PixelPoint? _lastPrimaryAnchor;
    private PixelPoint? _lastSecondaryAnchor;
    private Point _middleDragStartScreenPoint;
    private Vector _middleDragStartOffset;

    protected override void OnDataContextChanged(EventArgs e)
    {
        Unsubscribe(_viewModel);
        _viewModel = DataContext as RoomEditorViewModel;
        Subscribe(_viewModel);
        InvalidateVisual();
        base.OnDataContextChanged(e);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        if (_viewModel is null)
        {
            return;
        }

        var point = e.GetPosition(this);
        _hoverPoint = point;
        var currentPoint = e.GetCurrentPoint(this);

        if (currentPoint.Properties.IsMiddleButtonPressed)
        {
            _isMiddleDragging = true;
            _hoverPoint = null;
            _middleDragStartScreenPoint = GetPointerScreenPoint(e);
            _middleDragStartOffset = FindScrollViewer()?.Offset ?? default;
            e.Pointer.Capture(this);
            e.Handled = true;
            return;
        }

        if (currentPoint.Properties.IsLeftButtonPressed)
        {
            _isPrimaryDragging = true;
            _lastPrimaryAnchor = null;
            ApplyPrimary(point);
            e.Pointer.Capture(this);
            e.Handled = true;
        }

        if (currentPoint.Properties.IsRightButtonPressed)
        {
            _isSecondaryDragging = true;
            _lastSecondaryAnchor = null;
            ApplySecondary(point);
            e.Pointer.Capture(this);
            e.Handled = true;
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        if (_viewModel is null)
        {
            return;
        }

        var point = e.GetPosition(this);

        if (_isMiddleDragging && e.GetCurrentPoint(this).Properties.IsMiddleButtonPressed)
        {
            _hoverPoint = null;
            PanViewport(e);
            InvalidateVisual();
            return;
        }

        _hoverPoint = point;

        if (_isPrimaryDragging && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            ApplyPrimary(point);
        }

        if (_isSecondaryDragging && e.GetCurrentPoint(this).Properties.IsRightButtonPressed)
        {
            ApplySecondary(point);
        }

        InvalidateVisual();
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

        if (e.InitialPressMouseButton == MouseButton.Left)
        {
            _isPrimaryDragging = false;
            _lastPrimaryAnchor = null;
        }
        else if (e.InitialPressMouseButton == MouseButton.Middle)
        {
            _isMiddleDragging = false;
        }
        else if (e.InitialPressMouseButton == MouseButton.Right)
        {
            _isSecondaryDragging = false;
            _lastSecondaryAnchor = null;
        }

        if (!_isPrimaryDragging && !_isSecondaryDragging && !_isMiddleDragging)
        {
            e.Pointer.Capture(null);
        }
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _hoverPoint = null;
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        context.DrawRectangle(CanvasBackgroundBrush, null, new Rect(Bounds.Size));

        var viewModel = _viewModel;
        if (viewModel is null)
        {
            return;
        }

        var roomRect = new Rect(0, 0, Bounds.Width, Bounds.Height);
        if (viewModel.ShowColour)
        {
            context.DrawRectangle(new SolidColorBrush(ToAvaloniaColor(viewModel.Colour)), null, roomRect);
        }

        var visibleRect = GetVisibleRect(roomRect);

        if (viewModel.ShowBackgrounds)
        {
            foreach (var background in viewModel.BackgroundLayers.Where(static background => background.Visible && !background.Foreground))
            {
                DrawRoomBackground(context, background, visibleRect, roomRect);
            }
        }

        if (viewModel.ShowTiles)
        {
            foreach (var tile in viewModel.OrderedTiles)
            {
                var tileBounds = RoomEditorViewModel.GetTileBounds(tile);
                if (!tileBounds.Intersects(visibleRect))
                {
                    continue;
                }

                DrawTile(context, tile, tileBounds);
            }
        }

        if (viewModel.ShowObjects)
        {
            foreach (var roomInstance in viewModel.OrderedInstances)
            {
                var instanceBounds = RoomEditorViewModel.GetInstanceBounds(roomInstance);
                if (!instanceBounds.Intersects(visibleRect))
                {
                    continue;
                }

                DrawInstance(context, roomInstance, instanceBounds);
            }
        }

        if (viewModel.ShowGrid)
        {
            DrawGrid(context, viewModel, visibleRect);
        }

        context.DrawRectangle(null, RoomOutlinePen, new Rect(0.5, 0.5, Math.Max(0, Bounds.Width - 1), Math.Max(0, Bounds.Height - 1)));
        DrawHoverPreview(context, viewModel);
    }

    private void ApplyPrimary(Point point)
    {
        if (_viewModel is null || !_viewModel.TrySnapToGrid(point, out var x, out var y))
        {
            return;
        }

        var anchor = new PixelPoint(x, y);
        if (_lastPrimaryAnchor == anchor)
        {
            return;
        }

        _lastPrimaryAnchor = anchor;
        _viewModel.ApplyPrimaryTool(point);
    }

    private void ApplySecondary(Point point)
    {
        if (_viewModel is null)
        {
            return;
        }

        if (!_viewModel.TrySnapToGrid(point, out var x, out var y))
        {
            return;
        }

        var anchor = new PixelPoint(x, y);
        if (_lastSecondaryAnchor == anchor)
        {
            return;
        }

        _lastSecondaryAnchor = anchor;
        _viewModel.ApplySecondaryTool(point);
    }

    private void PanViewport(PointerEventArgs e)
    {
        var scrollViewer = FindScrollViewer();
        if (scrollViewer is null)
        {
            return;
        }

        var delta = GetPointerScreenPoint(e) - _middleDragStartScreenPoint;
        var targetOffset = new Vector(
            _middleDragStartOffset.X - delta.X,
            _middleDragStartOffset.Y - delta.Y);

        var maxX = Math.Max(0, scrollViewer.Extent.Width - scrollViewer.Viewport.Width);
        var maxY = Math.Max(0, scrollViewer.Extent.Height - scrollViewer.Viewport.Height);

        scrollViewer.Offset = new Vector(
            Math.Clamp(targetOffset.X, 0, maxX),
            Math.Clamp(targetOffset.Y, 0, maxY));
    }

    private void DrawRoomBackground(DrawingContext context, RoomBackground roomBackground, Rect visibleRect, Rect roomRect)
    {
        var bitmap = roomBackground.Background?.Bitmap;
        if (bitmap is null)
        {
            return;
        }

        var sourceRect = new Rect(0, 0, bitmap.PixelSize.Width, bitmap.PixelSize.Height);

        if (roomBackground.Stretch)
        {
            context.DrawImage(bitmap, sourceRect, roomRect);
            return;
        }

        var tileWidth = bitmap.PixelSize.Width;
        var tileHeight = bitmap.PixelSize.Height;
        if (tileWidth <= 0 || tileHeight <= 0)
        {
            return;
        }

        var startX = roomBackground.HTiled
            ? roomBackground.X + (int)Math.Floor((visibleRect.X - roomBackground.X) / tileWidth) * tileWidth
            : roomBackground.X;
        var startY = roomBackground.VTiled
            ? roomBackground.Y + (int)Math.Floor((visibleRect.Y - roomBackground.Y) / tileHeight) * tileHeight
            : roomBackground.Y;
        var endX = roomBackground.HTiled ? visibleRect.Right + tileWidth : roomBackground.X + tileWidth;
        var endY = roomBackground.VTiled ? visibleRect.Bottom + tileHeight : roomBackground.Y + tileHeight;

        for (var drawY = startY; drawY < endY; drawY += roomBackground.VTiled ? tileHeight : Math.Max(tileHeight, 1))
        {
            for (var drawX = startX; drawX < endX; drawX += roomBackground.HTiled ? tileWidth : Math.Max(tileWidth, 1))
            {
                var destinationRect = new Rect(drawX, drawY, tileWidth, tileHeight);
                if (destinationRect.Intersects(visibleRect))
                {
                    context.DrawImage(bitmap, sourceRect, destinationRect);
                }

                if (!roomBackground.HTiled)
                {
                    break;
                }
            }

            if (!roomBackground.VTiled)
            {
                break;
            }
        }
    }

    private static void DrawTile(DrawingContext context, RoomTile tile, Rect destinationRect)
    {
        var bitmap = tile.Background?.Bitmap;
        if (bitmap is null)
        {
            context.DrawRectangle(PlaceholderFillBrush, PlaceholderPen, destinationRect);
            return;
        }

        var sourceRect = new Rect(
            Math.Clamp(tile.SourceX, 0, Math.Max(0, bitmap.PixelSize.Width - 1)),
            Math.Clamp(tile.SourceY, 0, Math.Max(0, bitmap.PixelSize.Height - 1)),
            Math.Min(tile.Width, bitmap.PixelSize.Width - Math.Clamp(tile.SourceX, 0, bitmap.PixelSize.Width)),
            Math.Min(tile.Height, bitmap.PixelSize.Height - Math.Clamp(tile.SourceY, 0, bitmap.PixelSize.Height)));

        if (sourceRect.Width <= 0 || sourceRect.Height <= 0)
        {
            context.DrawRectangle(PlaceholderFillBrush, PlaceholderPen, destinationRect);
            return;
        }

        context.DrawImage(bitmap, sourceRect, destinationRect);
    }

    private static void DrawInstance(DrawingContext context, RoomInstance roomInstance, Rect destinationRect)
    {
        var bitmap = RoomEditorViewModel.GetObjectPreviewBitmap(roomInstance.Object);
        if (bitmap is null)
        {
            context.DrawRectangle(PlaceholderFillBrush, PlaceholderPen, destinationRect);
            return;
        }

        var sourceRect = new Rect(0, 0, bitmap.PixelSize.Width, bitmap.PixelSize.Height);
        context.DrawImage(bitmap, sourceRect, destinationRect);
    }

    private static void DrawGrid(DrawingContext context, RoomEditorViewModel viewModel, Rect visibleRect)
    {
        var hSnap = Math.Max(1, viewModel.HSnap);
        var vSnap = Math.Max(1, viewModel.VSnap);

        if (visibleRect.Width / hSnap > 256 || visibleRect.Height / vSnap > 256)
        {
            return;
        }

        var startX = (int)Math.Floor(visibleRect.X / hSnap) * hSnap;
        var endX = (int)Math.Ceiling(visibleRect.Right / hSnap) * hSnap;
        for (var x = startX; x <= endX; x += hSnap)
        {
            context.DrawLine(GridPen, new Point(x + 0.5, visibleRect.Y), new Point(x + 0.5, visibleRect.Bottom));
        }

        var startY = (int)Math.Floor(visibleRect.Y / vSnap) * vSnap;
        var endY = (int)Math.Ceiling(visibleRect.Bottom / vSnap) * vSnap;
        for (var y = startY; y <= endY; y += vSnap)
        {
            context.DrawLine(GridPen, new Point(visibleRect.X, y + 0.5), new Point(visibleRect.Right, y + 0.5));
        }
    }

    private void DrawHoverPreview(DrawingContext context, RoomEditorViewModel viewModel)
    {
        if (_isMiddleDragging)
        {
            return;
        }

        if (_hoverPoint is null || !viewModel.TrySnapToGrid(_hoverPoint.Value, out var snappedX, out var snappedY))
        {
            return;
        }

        if (_isSecondaryDragging)
        {
            var eraseRect = new Rect(snappedX, snappedY, Math.Max(1, viewModel.HSnap), Math.Max(1, viewModel.VSnap));
            context.DrawRectangle(null, ErasePen, eraseRect);
            return;
        }

        if (viewModel.ActiveLayer == RoomEditLayer.Instances)
        {
            var gameObject = viewModel.GetCurrentPlacementObject();
            if (gameObject is null)
            {
                return;
            }

            var previewInstance = new RoomInstance
            {
                Object = gameObject,
                X = snappedX,
                Y = snappedY,
            };
            var bounds = RoomEditorViewModel.GetInstanceBounds(previewInstance);
            var bitmap = RoomEditorViewModel.GetObjectPreviewBitmap(gameObject);
            if (bitmap is not null)
            {
                using (context.PushOpacity(0.55))
                {
                    context.DrawImage(bitmap, new Rect(0, 0, bitmap.PixelSize.Width, bitmap.PixelSize.Height), bounds);
                }
            }

            context.DrawRectangle(HoverFillBrush, HoverPen, bounds);
            return;
        }

        var tileBackground = viewModel.SelectedBackgroundOption?.Resource;
        if (tileBackground?.Bitmap is null)
        {
            return;
        }

        var tileRect = new Rect(snappedX, snappedY, Math.Max(1, viewModel.TileWidth), Math.Max(1, viewModel.TileHeight));
        var sourceRect = viewModel.GetTilePreviewSourceRect();
        using (context.PushOpacity(0.65))
        {
            context.DrawImage(tileBackground.Bitmap, sourceRect, tileRect);
        }
        context.DrawRectangle(HoverFillBrush, HoverPen, tileRect);
    }

    private Rect GetVisibleRect(Rect fallback)
    {
        var scrollViewer = FindScrollViewer();
        if (scrollViewer is null || scrollViewer.Viewport.Width <= 0 || scrollViewer.Viewport.Height <= 0)
        {
            return fallback;
        }

        return new Rect(scrollViewer.Offset.X, scrollViewer.Offset.Y, scrollViewer.Viewport.Width, scrollViewer.Viewport.Height);
    }

    private static Color ToAvaloniaColor(int rgb)
    {
        return Color.FromRgb(
            (byte)((rgb >> 16) & 0xFF),
            (byte)((rgb >> 8) & 0xFF),
            (byte)(rgb & 0xFF));
    }

    private ScrollViewer? FindScrollViewer()
    {
        return this.GetVisualAncestors().OfType<ScrollViewer>().FirstOrDefault();
    }

    private Point GetPointerScreenPoint(PointerEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        return topLevel is null ? e.GetPosition(this) : e.GetPosition(topLevel);
    }

    private void Subscribe(RoomEditorViewModel? viewModel)
    {
        if (viewModel is null)
        {
            return;
        }

        viewModel.PropertyChanged += ViewModelOnPropertyChanged;
        viewModel.CanvasChanged += ViewModelOnCanvasChanged;
    }

    private void Unsubscribe(RoomEditorViewModel? viewModel)
    {
        if (viewModel is null)
        {
            return;
        }

        viewModel.PropertyChanged -= ViewModelOnPropertyChanged;
        viewModel.CanvasChanged -= ViewModelOnCanvasChanged;
    }

    private void ViewModelOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        InvalidateVisual();
    }

    private void ViewModelOnCanvasChanged(object? sender, EventArgs e)
    {
        InvalidateVisual();
    }
}
