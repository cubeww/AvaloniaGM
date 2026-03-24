using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia;
using Avalonia.Media.Imaging;
using AvaloniaGM.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AvaloniaGM.ViewModels;

public enum RoomEditLayer
{
    Instances,
    Tiles,
}

public partial class RoomEditorViewModel : ObservableObject
{
    private readonly Room _room;
    private readonly Action<string> _appendOutput;
    private readonly Func<GameObject?> _getSelectedTreeObject;
    private readonly List<RoomInstance> _orderedInstances = [];
    private readonly List<RoomTile> _orderedTiles = [];
    private int _nextInstanceId;
    private int _nextTileId;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanvasWidth))]
    private int width;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanvasHeight))]
    private int height;

    [ObservableProperty]
    private int hSnap;

    [ObservableProperty]
    private int vSnap;

    [ObservableProperty]
    private int speed;

    [ObservableProperty]
    private bool persistent;

    [ObservableProperty]
    private bool showColour;

    [ObservableProperty]
    private int colour;

    [ObservableProperty]
    private string caption;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PlacementHint))]
    [NotifyPropertyChangedFor(nameof(IsEditingInstances))]
    [NotifyPropertyChangedFor(nameof(IsEditingTiles))]
    private RoomEditLayer activeLayer = RoomEditLayer.Instances;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedObject))]
    private ResourceReferenceOption<GameObject>? selectedObjectOption;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedBackground))]
    [NotifyPropertyChangedFor(nameof(SelectedBackgroundBitmap))]
    [NotifyPropertyChangedFor(nameof(SelectedBackgroundDisplayName))]
    private ResourceReferenceOption<Background>? selectedBackgroundOption;

    [ObservableProperty]
    private int tileSourceX;

    [ObservableProperty]
    private int tileSourceY;

    [ObservableProperty]
    private int tileWidth = 16;

    [ObservableProperty]
    private int tileHeight = 16;

    [ObservableProperty]
    private int tileDepth = 1000000;

    [ObservableProperty]
    private bool showGrid;

    [ObservableProperty]
    private bool showObjects;

    [ObservableProperty]
    private bool showTiles;

    [ObservableProperty]
    private bool showBackgrounds;

    [ObservableProperty]
    private bool deleteUnderlyingObj;

    [ObservableProperty]
    private bool deleteUnderlyingTiles;

    public string Name => _room.Name;

    public double CanvasWidth => Math.Max(1, Width);

    public double CanvasHeight => Math.Max(1, Height);

    public Array LayerOptions { get; } = Enum.GetValues<RoomEditLayer>();

    public ObservableCollection<ResourceReferenceOption<GameObject>> ObjectOptions { get; } = new();

    public ObservableCollection<ResourceReferenceOption<Background>> BackgroundOptions { get; } = new();

    public IReadOnlyList<RoomBackground> BackgroundLayers => _room.Backgrounds;

    public IReadOnlyList<RoomInstance> OrderedInstances => _orderedInstances;

    public IReadOnlyList<RoomTile> OrderedTiles => _orderedTiles;

    public int InstanceCount => _room.Instances.Count;

    public int TileCount => _room.Tiles.Count;

    public bool IsEditingInstances => ActiveLayer == RoomEditLayer.Instances;

    public bool IsEditingTiles => ActiveLayer == RoomEditLayer.Tiles;

    public bool HasSelectedObject => SelectedObjectOption?.Resource is not null;

    public bool HasSelectedBackground => SelectedBackgroundOption?.Resource is not null;

    public Bitmap? SelectedBackgroundBitmap => SelectedBackgroundOption?.Resource?.Bitmap;

    public string SelectedBackgroundDisplayName => SelectedBackgroundOption?.DisplayName ?? "<none>";

    public string PlacementHint => ActiveLayer == RoomEditLayer.Instances
        ? "Left click to place an instance. Right click to remove the topmost instance."
        : "Left click to paint a tile. Right click to remove the topmost tile.";

    public event EventHandler? CanvasChanged;

    public RoomEditorViewModel(Project project, Room room, Action<string> appendOutput, Func<GameObject?> getSelectedTreeObject)
    {
        _room = room;
        _appendOutput = appendOutput;
        _getSelectedTreeObject = getSelectedTreeObject;

        width = room.Width;
        height = room.Height;
        hSnap = room.HSnap;
        vSnap = room.VSnap;
        speed = room.Speed;
        persistent = room.Persistent;
        showColour = room.ShowColour;
        colour = room.Colour;
        caption = room.Caption;
        showGrid = room.MakerSettings.ShowGrid;
        showObjects = room.MakerSettings.ShowObjects;
        showTiles = room.MakerSettings.ShowTiles;
        showBackgrounds = room.MakerSettings.ShowBackgrounds;
        deleteUnderlyingObj = room.MakerSettings.DeleteUnderlyingObj;
        deleteUnderlyingTiles = room.MakerSettings.DeleteUnderlyingTiles;

        if (!_room.MakerSettings.ShowObjects && !_room.MakerSettings.ShowTiles && !_room.MakerSettings.ShowBackgrounds)
        {
            showObjects = true;
            showTiles = true;
            showBackgrounds = true;
        }

        PopulateResourceOptions(project);
        RebuildRenderCaches();

        _nextInstanceId = Math.Max(100000, _room.Instances.Count == 0 ? 100000 : _room.Instances.Max(static instance => instance.Id) + 1);
        _nextTileId = Math.Max(10000000, _room.Tiles.Count == 0 ? 10000000 : _room.Tiles.Max(static tile => tile.Id) + 1);
    }

    partial void OnWidthChanged(int value)
    {
        if (value <= 0)
        {
            Width = 1;
            return;
        }

        _room.Width = Math.Max(1, value);
        if (_room.PhysicsWorldRight < _room.Width)
        {
            _room.PhysicsWorldRight = _room.Width;
        }

        OnPropertyChanged(nameof(CanvasWidth));
        InvalidateCanvas();
    }

    partial void OnHeightChanged(int value)
    {
        if (value <= 0)
        {
            Height = 1;
            return;
        }

        _room.Height = Math.Max(1, value);
        if (_room.PhysicsWorldBottom < _room.Height)
        {
            _room.PhysicsWorldBottom = _room.Height;
        }

        OnPropertyChanged(nameof(CanvasHeight));
        InvalidateCanvas();
    }

    partial void OnHSnapChanged(int value)
    {
        if (value <= 0)
        {
            HSnap = 1;
            return;
        }

        _room.HSnap = Math.Max(1, value);
        InvalidateCanvas();
    }

    partial void OnVSnapChanged(int value)
    {
        if (value <= 0)
        {
            VSnap = 1;
            return;
        }

        _room.VSnap = Math.Max(1, value);
        InvalidateCanvas();
    }

    partial void OnSpeedChanged(int value) => _room.Speed = value;

    partial void OnPersistentChanged(bool value) => _room.Persistent = value;

    partial void OnShowColourChanged(bool value)
    {
        _room.ShowColour = value;
        InvalidateCanvas();
    }

    partial void OnColourChanged(int value)
    {
        _room.Colour = value;
        InvalidateCanvas();
    }

    partial void OnCaptionChanged(string value) => _room.Caption = value;

    partial void OnShowGridChanged(bool value)
    {
        _room.MakerSettings.ShowGrid = value;
        InvalidateCanvas();
    }

    partial void OnShowObjectsChanged(bool value)
    {
        _room.MakerSettings.ShowObjects = value;
        InvalidateCanvas();
    }

    partial void OnShowTilesChanged(bool value)
    {
        _room.MakerSettings.ShowTiles = value;
        InvalidateCanvas();
    }

    partial void OnShowBackgroundsChanged(bool value)
    {
        _room.MakerSettings.ShowBackgrounds = value;
        InvalidateCanvas();
    }

    partial void OnDeleteUnderlyingObjChanged(bool value) => _room.MakerSettings.DeleteUnderlyingObj = value;

    partial void OnDeleteUnderlyingTilesChanged(bool value) => _room.MakerSettings.DeleteUnderlyingTiles = value;

    partial void OnActiveLayerChanged(RoomEditLayer value)
    {
        OnPropertyChanged(nameof(IsEditingInstances));
        OnPropertyChanged(nameof(IsEditingTiles));
        OnPropertyChanged(nameof(PlacementHint));
        InvalidateCanvas();
    }

    partial void OnSelectedObjectOptionChanged(ResourceReferenceOption<GameObject>? value)
    {
        OnPropertyChanged(nameof(HasSelectedObject));
        InvalidateCanvas();
    }

    partial void OnSelectedBackgroundOptionChanged(ResourceReferenceOption<Background>? value)
    {
        if (value?.Resource is not null)
        {
            ApplyBackgroundDefaults(value.Resource);
        }

        OnPropertyChanged(nameof(HasSelectedBackground));
        OnPropertyChanged(nameof(SelectedBackgroundBitmap));
        OnPropertyChanged(nameof(SelectedBackgroundDisplayName));
        InvalidateCanvas();
    }

    partial void OnTileSourceXChanged(int value)
    {
        if (value < 0)
        {
            TileSourceX = 0;
            return;
        }

        InvalidateCanvas();
    }

    partial void OnTileSourceYChanged(int value)
    {
        if (value < 0)
        {
            TileSourceY = 0;
            return;
        }

        InvalidateCanvas();
    }

    partial void OnTileWidthChanged(int value)
    {
        if (value <= 0)
        {
            TileWidth = 1;
            return;
        }

        InvalidateCanvas();
    }

    partial void OnTileHeightChanged(int value)
    {
        if (value <= 0)
        {
            TileHeight = 1;
            return;
        }

        InvalidateCanvas();
    }

    partial void OnTileDepthChanged(int value) => InvalidateCanvas();

    public bool TrySnapToGrid(Point roomPoint, out int snappedX, out int snappedY)
    {
        snappedX = 0;
        snappedY = 0;

        if (roomPoint.X < 0 || roomPoint.Y < 0 || roomPoint.X > Width || roomPoint.Y > Height)
        {
            return false;
        }

        var gridWidth = Math.Max(1, HSnap);
        var gridHeight = Math.Max(1, VSnap);
        snappedX = (int)Math.Floor(roomPoint.X / gridWidth) * gridWidth;
        snappedY = (int)Math.Floor(roomPoint.Y / gridHeight) * gridHeight;
        return true;
    }

    public void ApplyPrimaryTool(Point roomPoint)
    {
        if (!TrySnapToGrid(roomPoint, out var snappedX, out var snappedY))
        {
            return;
        }

        switch (ActiveLayer)
        {
            case RoomEditLayer.Instances:
                PlaceInstance(snappedX, snappedY);
                break;
            case RoomEditLayer.Tiles:
                PaintTile(snappedX, snappedY);
                break;
        }
    }

    public void ApplySecondaryTool(Point roomPoint)
    {
        if (roomPoint.X < 0 || roomPoint.Y < 0 || roomPoint.X > Width || roomPoint.Y > Height)
        {
            return;
        }

        switch (ActiveLayer)
        {
            case RoomEditLayer.Instances:
                RemoveTopmostInstance(roomPoint);
                break;
            case RoomEditLayer.Tiles:
                RemoveTopmostTile(roomPoint);
                break;
        }
    }

    public void SelectTileSourceAtPixel(int pixelX, int pixelY)
    {
        var background = SelectedBackgroundOption?.Resource;
        var bitmap = background?.Bitmap;
        if (background is null || bitmap is null)
        {
            return;
        }

        var sourceTileWidth = GetEffectiveSourceTileWidth(background);
        var sourceTileHeight = GetEffectiveSourceTileHeight(background);
        var strideX = sourceTileWidth + Math.Max(0, background.TileHorizontalSeparation);
        var strideY = sourceTileHeight + Math.Max(0, background.TileVerticalSeparation);
        var offsetX = Math.Max(0, background.TileXOffset);
        var offsetY = Math.Max(0, background.TileYOffset);

        if (pixelX < offsetX || pixelY < offsetY)
        {
            return;
        }

        var column = (pixelX - offsetX) / Math.Max(1, strideX);
        var row = (pixelY - offsetY) / Math.Max(1, strideY);
        var nextSourceX = offsetX + column * strideX;
        var nextSourceY = offsetY + row * strideY;

        if (nextSourceX + sourceTileWidth > bitmap.PixelSize.Width
            || nextSourceY + sourceTileHeight > bitmap.PixelSize.Height)
        {
            return;
        }

        TileSourceX = nextSourceX;
        TileSourceY = nextSourceY;
    }

    public Rect GetTilePreviewSourceRect()
    {
        return new Rect(TileSourceX, TileSourceY, Math.Max(1, TileWidth), Math.Max(1, TileHeight));
    }

    public GameObject? GetCurrentPlacementObject()
    {
        return _getSelectedTreeObject() ?? SelectedObjectOption?.Resource;
    }

    public void NotifyPlacementSourceChanged()
    {
        SyncSelectedObjectOptionWithTreeSelection();
        InvalidateCanvas();
    }

    public static Bitmap? GetObjectPreviewBitmap(GameObject? gameObject)
    {
        return gameObject?.Sprite?.Frames
            .OrderBy(static frame => frame.Index)
            .Select(static frame => frame.Bitmap)
            .FirstOrDefault(static bitmap => bitmap is not null);
    }

    public static Rect GetInstanceBounds(RoomInstance instance)
    {
        var bitmap = GetObjectPreviewBitmap(instance.Object);
        var sprite = instance.Object?.Sprite;

        if (bitmap is null || sprite is null)
        {
            return new Rect(instance.X - 8, instance.Y - 8, 16, 16);
        }

        var width = Math.Max(1, bitmap.PixelSize.Width * Math.Abs(instance.ScaleX));
        var height = Math.Max(1, bitmap.PixelSize.Height * Math.Abs(instance.ScaleY));
        var originX = sprite.XOrigin * Math.Abs(instance.ScaleX);
        var originY = sprite.YOrigin * Math.Abs(instance.ScaleY);

        return new Rect(instance.X - originX, instance.Y - originY, width, height);
    }

    public static Rect GetTileBounds(RoomTile tile)
    {
        return new Rect(
            tile.X,
            tile.Y,
            Math.Max(1, tile.Width * Math.Abs(tile.ScaleX)),
            Math.Max(1, tile.Height * Math.Abs(tile.ScaleY)));
    }

    private void PopulateResourceOptions(Project project)
    {
        ObjectOptions.Clear();
        ObjectOptions.Add(new ResourceReferenceOption<GameObject>("<none>", null));
        foreach (var gameObject in project.Objects.OrderBy(static objectItem => objectItem.Name, StringComparer.OrdinalIgnoreCase))
        {
            ObjectOptions.Add(new ResourceReferenceOption<GameObject>(gameObject.Name, gameObject));
        }

        BackgroundOptions.Clear();
        BackgroundOptions.Add(new ResourceReferenceOption<Background>("<none>", null));
        foreach (var background in project.Backgrounds
                     .Where(static background => background.Bitmap is not null)
                     .OrderBy(static background => background.Name, StringComparer.OrdinalIgnoreCase))
        {
            BackgroundOptions.Add(new ResourceReferenceOption<Background>(background.Name, background));
        }

        SelectedObjectOption = ObjectOptions.FirstOrDefault(option => option.Resource is not null) ?? ObjectOptions.FirstOrDefault();
        SelectedBackgroundOption = BackgroundOptions.FirstOrDefault(option => option.Resource?.Bitmap is not null) ?? BackgroundOptions.FirstOrDefault();

        if (SelectedBackgroundOption?.Resource is not null)
        {
            ApplyBackgroundDefaults(SelectedBackgroundOption.Resource);
        }
    }

    private void ApplyBackgroundDefaults(Background background)
    {
        var sourceTileWidth = GetEffectiveSourceTileWidth(background);
        var sourceTileHeight = GetEffectiveSourceTileHeight(background);

        TileWidth = sourceTileWidth;
        TileHeight = sourceTileHeight;
        TileSourceX = Math.Max(0, background.TileXOffset);
        TileSourceY = Math.Max(0, background.TileYOffset);
    }

    private static int GetEffectiveSourceTileWidth(Background background)
    {
        return Math.Max(1, background.IsTileset ? background.TileWidth : background.Width);
    }

    private static int GetEffectiveSourceTileHeight(Background background)
    {
        return Math.Max(1, background.IsTileset ? background.TileHeight : background.Height);
    }

    private void PlaceInstance(int x, int y)
    {
        SyncSelectedObjectOptionWithTreeSelection();
        var gameObject = GetCurrentPlacementObject();
        if (gameObject is null)
        {
            return;
        }

        if (DeleteUnderlyingObj)
        {
            RemoveInstancesAtAnchor(x, y);
        }

        var instanceId = _nextInstanceId++;
        var roomInstance = new RoomInstance
        {
            Id = instanceId,
            Name = $"inst_{instanceId:X}",
            Object = gameObject,
            X = x,
            Y = y,
        };

        _room.Instances.Add(roomInstance);
        RebuildRenderCaches();
        NotifySceneCountsChanged();
        InvalidateCanvas();
    }

    private void SyncSelectedObjectOptionWithTreeSelection()
    {
        var selectedTreeObject = _getSelectedTreeObject();
        if (selectedTreeObject is null)
        {
            return;
        }

        if (!ReferenceEquals(SelectedObjectOption?.Resource, selectedTreeObject))
        {
            var matchingOption = ObjectOptions.FirstOrDefault(option => ReferenceEquals(option.Resource, selectedTreeObject));
            if (matchingOption is not null)
            {
                SelectedObjectOption = matchingOption;
            }
        }
    }

    private void PaintTile(int x, int y)
    {
        var background = SelectedBackgroundOption?.Resource;
        if (background?.Bitmap is null)
        {
            return;
        }

        if (DeleteUnderlyingTiles)
        {
            RemoveTilesAtAnchor(x, y);
        }

        var tileId = _nextTileId++;
        var roomTile = new RoomTile
        {
            Id = tileId,
            Background = background,
            X = x,
            Y = y,
            Width = Math.Max(1, TileWidth),
            Height = Math.Max(1, TileHeight),
            SourceX = Math.Max(0, TileSourceX),
            SourceY = Math.Max(0, TileSourceY),
            Depth = TileDepth,
        };

        _room.Tiles.Add(roomTile);
        RebuildRenderCaches();
        NotifySceneCountsChanged();
        InvalidateCanvas();
    }

    private void RemoveTopmostInstance(Point roomPoint)
    {
        for (var index = _orderedInstances.Count - 1; index >= 0; index--)
        {
            var roomInstance = _orderedInstances[index];
            if (!GetInstanceBounds(roomInstance).Contains(roomPoint))
            {
                continue;
            }

            _room.Instances.Remove(roomInstance);
            RebuildRenderCaches();
            NotifySceneCountsChanged();
            InvalidateCanvas();
            return;
        }
    }

    private void RemoveTopmostTile(Point roomPoint)
    {
        for (var index = _orderedTiles.Count - 1; index >= 0; index--)
        {
            var roomTile = _orderedTiles[index];
            if (!GetTileBounds(roomTile).Contains(roomPoint))
            {
                continue;
            }

            _room.Tiles.Remove(roomTile);
            RebuildRenderCaches();
            NotifySceneCountsChanged();
            InvalidateCanvas();
            return;
        }
    }

    private void RemoveInstancesAtAnchor(int x, int y)
    {
        for (var index = _room.Instances.Count - 1; index >= 0; index--)
        {
            var roomInstance = _room.Instances[index];
            if (roomInstance.X == x && roomInstance.Y == y)
            {
                _room.Instances.RemoveAt(index);
            }
        }
    }

    private void RemoveTilesAtAnchor(int x, int y)
    {
        for (var index = _room.Tiles.Count - 1; index >= 0; index--)
        {
            var roomTile = _room.Tiles[index];
            if (roomTile.X == x && roomTile.Y == y)
            {
                _room.Tiles.RemoveAt(index);
            }
        }
    }

    private void RebuildRenderCaches()
    {
        _orderedInstances.Clear();
        _orderedInstances.AddRange(_room.Instances
            .OrderByDescending(static instance => instance.Object?.Depth ?? 0)
            .ThenBy(static instance => instance.Id));

        _orderedTiles.Clear();
        _orderedTiles.AddRange(_room.Tiles
            .OrderByDescending(static tile => tile.Depth)
            .ThenBy(static tile => tile.Id));
    }

    private void NotifySceneCountsChanged()
    {
        OnPropertyChanged(nameof(InstanceCount));
        OnPropertyChanged(nameof(TileCount));
    }

    private void InvalidateCanvas()
    {
        CanvasChanged?.Invoke(this, EventArgs.Empty);
    }
}
