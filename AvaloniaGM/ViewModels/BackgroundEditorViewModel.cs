using System;
using Avalonia.Media.Imaging;
using AvaloniaGM.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AvaloniaGM.ViewModels;

public partial class BackgroundEditorViewModel : ObservableObject
{
    private readonly Background _background;
    private readonly Action<Resource> _refreshResourceVisuals;
    private readonly Action<string> _appendOutput;

    [ObservableProperty]
    private bool isTileset;

    [ObservableProperty]
    private bool hTile;

    [ObservableProperty]
    private bool vTile;

    [ObservableProperty]
    private int tileWidth;

    [ObservableProperty]
    private int tileHeight;

    [ObservableProperty]
    private int tileXOffset;

    [ObservableProperty]
    private int tileYOffset;

    [ObservableProperty]
    private int tileHorizontalSeparation;

    [ObservableProperty]
    private int tileVerticalSeparation;

    [ObservableProperty]
    private bool for3D;

    [ObservableProperty]
    private bool dynamicTexturePage;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RemoveImageCommand))]
    [NotifyPropertyChangedFor(nameof(HasImage))]
    [NotifyPropertyChangedFor(nameof(HasNoImage))]
    [NotifyPropertyChangedFor(nameof(ImageStatusText))]
    [NotifyPropertyChangedFor(nameof(BackgroundWidth))]
    [NotifyPropertyChangedFor(nameof(BackgroundHeight))]
    [NotifyPropertyChangedFor(nameof(BackgroundSizeText))]
    private Bitmap? previewBitmap;

    public string Name => _background.Name;

    public bool HasImage => PreviewBitmap is not null;

    public bool HasNoImage => PreviewBitmap is null;

    public int BackgroundWidth => PreviewBitmap?.PixelSize.Width ?? _background.Width;

    public int BackgroundHeight => PreviewBitmap?.PixelSize.Height ?? _background.Height;

    public string BackgroundSizeText => $"{BackgroundWidth} x {BackgroundHeight}";

    public string ImageStatusText => PreviewBitmap is null
        ? "No background image selected"
        : $"Image loaded ({BackgroundWidth} x {BackgroundHeight})";

    public BackgroundEditorViewModel(Background background, Action<Resource> refreshResourceVisuals, Action<string> appendOutput)
    {
        _background = background;
        _refreshResourceVisuals = refreshResourceVisuals;
        _appendOutput = appendOutput;

        isTileset = background.IsTileset;
        hTile = background.HTile;
        vTile = background.VTile;
        tileWidth = background.TileWidth;
        tileHeight = background.TileHeight;
        tileXOffset = background.TileXOffset;
        tileYOffset = background.TileYOffset;
        tileHorizontalSeparation = background.TileHorizontalSeparation;
        tileVerticalSeparation = background.TileVerticalSeparation;
        for3D = background.For3D;
        dynamicTexturePage = background.DynamicTexturePage;
        previewBitmap = background.Bitmap;
    }

    partial void OnIsTilesetChanged(bool value) => _background.IsTileset = value;

    partial void OnHTileChanged(bool value) => _background.HTile = value;

    partial void OnVTileChanged(bool value) => _background.VTile = value;

    partial void OnTileWidthChanged(int value) => _background.TileWidth = value;

    partial void OnTileHeightChanged(int value) => _background.TileHeight = value;

    partial void OnTileXOffsetChanged(int value) => _background.TileXOffset = value;

    partial void OnTileYOffsetChanged(int value) => _background.TileYOffset = value;

    partial void OnTileHorizontalSeparationChanged(int value) => _background.TileHorizontalSeparation = value;

    partial void OnTileVerticalSeparationChanged(int value) => _background.TileVerticalSeparation = value;

    partial void OnFor3DChanged(bool value) => _background.For3D = value;

    partial void OnDynamicTexturePageChanged(bool value) => _background.DynamicTexturePage = value;

    public void SetImage(Bitmap bitmap)
    {
        ArgumentNullException.ThrowIfNull(bitmap);

        _background.Bitmap = bitmap;
        _background.Width = bitmap.PixelSize.Width;
        _background.Height = bitmap.PixelSize.Height;
        PreviewBitmap = bitmap;

        RefreshDerivedState();
        _appendOutput($"Imported image for background {Name}: {BackgroundSizeText}.");
    }

    public void NotifyImageImportFailed(string details)
    {
        _appendOutput($"Failed to import image for background {Name}: {details}");
    }

    [RelayCommand(CanExecute = nameof(CanRemoveImage))]
    private void RemoveImage()
    {
        _background.Bitmap = null;
        _background.Width = 0;
        _background.Height = 0;
        PreviewBitmap = null;

        RefreshDerivedState();
        _appendOutput($"Removed image from background {Name}.");
    }

    private bool CanRemoveImage() => PreviewBitmap is not null;

    private void RefreshDerivedState()
    {
        OnPropertyChanged(nameof(HasImage));
        OnPropertyChanged(nameof(HasNoImage));
        OnPropertyChanged(nameof(BackgroundWidth));
        OnPropertyChanged(nameof(BackgroundHeight));
        OnPropertyChanged(nameof(BackgroundSizeText));
        OnPropertyChanged(nameof(ImageStatusText));
        _refreshResourceVisuals(_background);
    }
}
