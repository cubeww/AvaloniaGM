using System;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using AvaloniaGM.ViewModels;

namespace AvaloniaGM.Views.Controls;

public class RoomTilesetPickerView : Control
{
    private static readonly IBrush BackgroundBrush = new SolidColorBrush(Color.Parse("#FF252525"));
    private static readonly Pen BorderPen = new(new SolidColorBrush(Color.Parse("#FF4A4A4A")), 1);
    private static readonly Pen SelectionPen = new(new SolidColorBrush(Color.Parse("#FF5FA8FF")), 2);
    private RoomEditorViewModel? _viewModel;

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

        var viewModel = _viewModel;
        var bitmap = viewModel?.SelectedBackgroundBitmap;
        if (viewModel is null || bitmap is null)
        {
            return;
        }

        var drawRect = GetBitmapDrawRect(bitmap);
        if (!drawRect.Contains(e.GetPosition(this)))
        {
            return;
        }

        var position = e.GetPosition(this);
        var scaleX = bitmap.PixelSize.Width / drawRect.Width;
        var scaleY = bitmap.PixelSize.Height / drawRect.Height;
        var pixelX = (int)((position.X - drawRect.X) * scaleX);
        var pixelY = (int)((position.Y - drawRect.Y) * scaleY);

        viewModel.SelectTileSourceAtPixel(pixelX, pixelY);
        e.Handled = true;
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        context.DrawRectangle(BackgroundBrush, BorderPen, new Rect(Bounds.Size));

        var viewModel = _viewModel;
        var bitmap = viewModel?.SelectedBackgroundBitmap;
        if (viewModel is null || bitmap is null || Bounds.Width <= 0 || Bounds.Height <= 0)
        {
            return;
        }

        var drawRect = GetBitmapDrawRect(bitmap);
        context.DrawImage(bitmap, new Rect(0, 0, bitmap.PixelSize.Width, bitmap.PixelSize.Height), drawRect);

        var selectionRect = viewModel.GetTilePreviewSourceRect();
        var scaleX = drawRect.Width / bitmap.PixelSize.Width;
        var scaleY = drawRect.Height / bitmap.PixelSize.Height;
        var visualSelectionRect = new Rect(
            drawRect.X + selectionRect.X * scaleX,
            drawRect.Y + selectionRect.Y * scaleY,
            Math.Max(1, selectionRect.Width * scaleX),
            Math.Max(1, selectionRect.Height * scaleY));

        context.DrawRectangle(null, SelectionPen, visualSelectionRect);
    }

    private Rect GetBitmapDrawRect(Bitmap bitmap)
    {
        var scale = Math.Min(Bounds.Width / bitmap.PixelSize.Width, Bounds.Height / bitmap.PixelSize.Height);
        scale = double.IsFinite(scale) && scale > 0 ? scale : 1.0;

        var width = bitmap.PixelSize.Width * scale;
        var height = bitmap.PixelSize.Height * scale;
        var x = (Bounds.Width - width) / 2;
        var y = (Bounds.Height - height) / 2;
        return new Rect(x, y, width, height);
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
