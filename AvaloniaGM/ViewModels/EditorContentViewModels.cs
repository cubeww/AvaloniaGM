using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Media.Imaging;
using AvaloniaGM.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AvaloniaGM.ViewModels;

public sealed class ResourceSummaryEditorViewModel
{
    public string Header { get; }

    public string Subtitle { get; }

    public string Content { get; }

    public ResourceSummaryEditorViewModel(string header, string subtitle, string content)
    {
        Header = header;
        Subtitle = subtitle;
        Content = content;
    }
}

public partial class SpriteEditorViewModel : ObservableObject
{
    private readonly Sprite _sprite;
    private readonly Action<Resource> _refreshResourceVisuals;
    private readonly Action<string> _appendOutput;

    [ObservableProperty]
    private SpriteType type;

    [ObservableProperty]
    private int xOrigin;

    [ObservableProperty]
    private int yOrigin;

    [ObservableProperty]
    private SpriteCollisionKind collisionKind;

    [ObservableProperty]
    private int collisionTolerance;

    [ObservableProperty]
    private bool separateCollisionMasks;

    [ObservableProperty]
    private SpriteBoundingBoxMode boundingBoxMode;

    [ObservableProperty]
    private bool hTile;

    [ObservableProperty]
    private bool vTile;

    [ObservableProperty]
    private bool for3D;

    [ObservableProperty]
    private bool dynamicTexturePage;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RemoveSelectedFrameCommand))]
    [NotifyPropertyChangedFor(nameof(HasSelectedFrame))]
    [NotifyPropertyChangedFor(nameof(HasNoSelectedFrame))]
    [NotifyPropertyChangedFor(nameof(SelectedFrameBitmap))]
    [NotifyPropertyChangedFor(nameof(SelectedFrameTitle))]
    private SpriteFrameItemViewModel? selectedFrame;

    public ObservableCollection<SpriteFrameItemViewModel> Frames { get; } = new();

    public string Name => _sprite.Name;

    public int SpriteWidth => _sprite.Width != 0
        ? _sprite.Width
        : _sprite.Frames.Select(static frame => frame.Width).FirstOrDefault(width => width > 0);

    public int SpriteHeight => _sprite.Height != 0
        ? _sprite.Height
        : _sprite.Frames.Select(static frame => frame.Height).FirstOrDefault(height => height > 0);

    public string SpriteSizeText => $"{SpriteWidth} x {SpriteHeight}";

    public int FrameCount => Frames.Count;

    public bool HasSelectedFrame => SelectedFrame is not null;

    public bool HasNoSelectedFrame => SelectedFrame is null;

    public Bitmap? SelectedFrameBitmap => SelectedFrame?.Bitmap;

    public string SelectedFrameTitle => SelectedFrame is null
        ? "No frame selected"
        : $"Frame {SelectedFrame.Index} ({SelectedFrame.Width} x {SelectedFrame.Height})";

    public Array SpriteTypes { get; } = Enum.GetValues<SpriteType>();

    public Array CollisionKinds { get; } = Enum.GetValues<SpriteCollisionKind>();

    public Array BoundingBoxModes { get; } = Enum.GetValues<SpriteBoundingBoxMode>();

    public SpriteEditorViewModel(Sprite sprite, Action<Resource> refreshResourceVisuals, Action<string> appendOutput)
    {
        _sprite = sprite;
        _refreshResourceVisuals = refreshResourceVisuals;
        _appendOutput = appendOutput;

        type = sprite.Type;
        xOrigin = sprite.XOrigin;
        yOrigin = sprite.YOrigin;
        collisionKind = sprite.CollisionKind;
        collisionTolerance = sprite.CollisionTolerance;
        separateCollisionMasks = sprite.SeparateCollisionMasks;
        boundingBoxMode = sprite.BoundingBoxMode;
        hTile = sprite.HTile;
        vTile = sprite.VTile;
        for3D = sprite.For3D;
        dynamicTexturePage = sprite.DynamicTexturePage;

        RebuildFrameItems(selectFrame: sprite.Frames.FirstOrDefault());
    }

    partial void OnTypeChanged(SpriteType value) => _sprite.Type = value;

    partial void OnXOriginChanged(int value) => _sprite.XOrigin = value;

    partial void OnYOriginChanged(int value) => _sprite.YOrigin = value;

    partial void OnCollisionKindChanged(SpriteCollisionKind value) => _sprite.CollisionKind = value;

    partial void OnCollisionToleranceChanged(int value) => _sprite.CollisionTolerance = value;

    partial void OnSeparateCollisionMasksChanged(bool value) => _sprite.SeparateCollisionMasks = value;

    partial void OnBoundingBoxModeChanged(SpriteBoundingBoxMode value) => _sprite.BoundingBoxMode = value;

    partial void OnHTileChanged(bool value) => _sprite.HTile = value;

    partial void OnVTileChanged(bool value) => _sprite.VTile = value;

    partial void OnFor3DChanged(bool value) => _sprite.For3D = value;

    partial void OnDynamicTexturePageChanged(bool value) => _sprite.DynamicTexturePage = value;

    public void AddFrames(IEnumerable<Bitmap> bitmaps)
    {
        ArgumentNullException.ThrowIfNull(bitmaps);

        var importedBitmaps = bitmaps.ToList();
        if (importedBitmaps.Count == 0)
        {
            return;
        }

        var hasExistingFrames = _sprite.Frames.Count > 0;
        var targetWidth = hasExistingFrames ? SpriteWidth : importedBitmaps[0].PixelSize.Width;
        var targetHeight = hasExistingFrames ? SpriteHeight : importedBitmaps[0].PixelSize.Height;
        var invalidBitmap = importedBitmaps.FirstOrDefault(bitmap =>
            bitmap.PixelSize.Width != targetWidth || bitmap.PixelSize.Height != targetHeight);

        if (invalidBitmap is not null)
        {
            _appendOutput(
                $"Rejected frame import for sprite {Name}: expected {targetWidth} x {targetHeight}, got {invalidBitmap.PixelSize.Width} x {invalidBitmap.PixelSize.Height}.");
            return;
        }

        var addedFrames = new List<SpriteFrame>();

        foreach (var bitmap in importedBitmaps)
        {
            var frame = new SpriteFrame
            {
                Index = _sprite.Frames.Count,
                Bitmap = bitmap,
            };

            _sprite.Frames.Add(frame);
            addedFrames.Add(frame);
        }

        if (addedFrames.Count == 0)
        {
            return;
        }

        RenumberFrames();
        SynchronizeSpriteDimensions();
        RebuildFrameItems(selectFrame: addedFrames[^1]);
        RefreshDerivedState();

        _appendOutput($"Added {addedFrames.Count} frame(s) to sprite {Name}.");
    }

    [RelayCommand(CanExecute = nameof(CanRemoveSelectedFrame))]
    private void RemoveSelectedFrame()
    {
        if (SelectedFrame?.Frame is null)
        {
            return;
        }

        var removedIndex = SelectedFrame.Frame.Index;
        var nextSelection = _sprite.Frames
            .Where(frame => !ReferenceEquals(frame, SelectedFrame.Frame))
            .OrderBy(static frame => frame.Index)
            .FirstOrDefault(frame => frame.Index >= removedIndex)
            ?? _sprite.Frames
                .Where(frame => !ReferenceEquals(frame, SelectedFrame.Frame))
                .OrderBy(static frame => frame.Index)
                .LastOrDefault();

        _sprite.Frames.Remove(SelectedFrame.Frame);

        RenumberFrames();
        SynchronizeSpriteDimensions();
        RebuildFrameItems(selectFrame: nextSelection);
        RefreshDerivedState();

        _appendOutput($"Removed frame {removedIndex} from sprite {Name}.");
    }

    private bool CanRemoveSelectedFrame() => SelectedFrame is not null;

    private void RenumberFrames()
    {
        for (var index = 0; index < _sprite.Frames.Count; index++)
        {
            _sprite.Frames[index].Index = index;
        }
    }

    private void SynchronizeSpriteDimensions()
    {
        var firstBitmap = _sprite.Frames
            .OrderBy(static frame => frame.Index)
            .Select(static frame => frame.Bitmap)
            .FirstOrDefault(static bitmap => bitmap is not null);

        if (firstBitmap is null)
        {
            return;
        }

        _sprite.Width = firstBitmap.PixelSize.Width;
        _sprite.Height = firstBitmap.PixelSize.Height;
    }

    private void RebuildFrameItems(SpriteFrame? selectFrame)
    {
        Frames.Clear();

        SpriteFrameItemViewModel? selectedFrameItem = null;

        foreach (var frame in _sprite.Frames.OrderBy(static frame => frame.Index))
        {
            var frameItem = new SpriteFrameItemViewModel(frame);
            Frames.Add(frameItem);

            if (ReferenceEquals(frame, selectFrame))
            {
                selectedFrameItem = frameItem;
            }
        }

        SelectedFrame = selectedFrameItem ?? Frames.FirstOrDefault();
    }

    private void RefreshDerivedState()
    {
        OnPropertyChanged(nameof(SpriteWidth));
        OnPropertyChanged(nameof(SpriteHeight));
        OnPropertyChanged(nameof(SpriteSizeText));
        OnPropertyChanged(nameof(FrameCount));
        OnPropertyChanged(nameof(HasSelectedFrame));
        OnPropertyChanged(nameof(HasNoSelectedFrame));
        OnPropertyChanged(nameof(SelectedFrameBitmap));
        OnPropertyChanged(nameof(SelectedFrameTitle));
        _refreshResourceVisuals(_sprite);
    }
}

public sealed class SpriteFrameItemViewModel
{
    public SpriteFrame Frame { get; }

    public int Index => Frame.Index;

    public Bitmap? Bitmap => Frame.Bitmap;

    public int Width => Frame.Width;

    public int Height => Frame.Height;

    public string Header => $"Frame {Index}";

    public string SizeText => $"{Width} x {Height}";

    public SpriteFrameItemViewModel(SpriteFrame frame)
    {
        Frame = frame;
    }
}
