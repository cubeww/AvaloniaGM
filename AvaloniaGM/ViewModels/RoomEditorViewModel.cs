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
    private bool _isSynchronizingSelectedInstanceEditor;
    private bool _isSynchronizingSelectedTileEditor;
    private RoomInstanceCodeDocumentViewModel? _selectedInstanceCodeDocument;

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

    [ObservableProperty]
    private bool enableViews;

    [ObservableProperty]
    private bool viewClearScreen;

    [ObservableProperty]
    private bool clearDisplayBuffer;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedInstance))]
    [NotifyPropertyChangedFor(nameof(SelectedInstanceObjectName))]
    [NotifyPropertyChangedFor(nameof(SelectedInstanceCodeDocument))]
    private RoomInstance? selectedInstance;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedTile))]
    [NotifyPropertyChangedFor(nameof(SelectedTileBackgroundName))]
    private RoomTile? selectedTile;

    [ObservableProperty]
    private int selectedInstanceX;

    [ObservableProperty]
    private int selectedInstanceY;

    [ObservableProperty]
    private double selectedInstanceScaleX = 1.0;

    [ObservableProperty]
    private double selectedInstanceScaleY = 1.0;

    [ObservableProperty]
    private double selectedInstanceRotation;

    [ObservableProperty]
    private int selectedTileX;

    [ObservableProperty]
    private int selectedTileY;

    [ObservableProperty]
    private int selectedTileWidth = 16;

    [ObservableProperty]
    private int selectedTileHeight = 16;

    [ObservableProperty]
    private int selectedTileSourceX;

    [ObservableProperty]
    private int selectedTileSourceY;

    [ObservableProperty]
    private double selectedTileScaleX = 1.0;

    [ObservableProperty]
    private double selectedTileScaleY = 1.0;

    [ObservableProperty]
    private int selectedTileDepth = 1000000;

    public string Name => _room.Name;

    public double CanvasWidth => Math.Max(1, Width);

    public double CanvasHeight => Math.Max(1, Height);

    public Array LayerOptions { get; } = Enum.GetValues<RoomEditLayer>();

    public ObservableCollection<ResourceReferenceOption<GameObject>> ObjectOptions { get; } = new();

    public ObservableCollection<ResourceReferenceOption<Background>> BackgroundOptions { get; } = new();

    public ObservableCollection<ResourceReferenceOption<Background>> RoomBackgroundOptions { get; } = new();

    public ObservableCollection<ResourceReferenceOption<GameObject>> ViewObjectOptions { get; } = new();

    public ObservableCollection<RoomBackgroundSlotViewModel> RoomBackgroundSlots { get; } = new();

    public ObservableCollection<RoomViewSlotViewModel> RoomViewSlots { get; } = new();

    public IReadOnlyList<RoomBackground> BackgroundLayers => _room.Backgrounds;

    public IReadOnlyList<RoomView> ViewLayers => _room.Views;

    public IReadOnlyList<RoomInstance> OrderedInstances => _orderedInstances;

    public IReadOnlyList<RoomTile> OrderedTiles => _orderedTiles;

    public int InstanceCount => _room.Instances.Count;

    public int TileCount => _room.Tiles.Count;

    public bool IsEditingInstances => ActiveLayer == RoomEditLayer.Instances;

    public bool IsEditingTiles => ActiveLayer == RoomEditLayer.Tiles;

    public bool HasSelectedObject => SelectedObjectOption?.Resource is not null;

    public bool HasSelectedBackground => SelectedBackgroundOption?.Resource is not null;

    public bool HasSelectedInstance => SelectedInstance is not null;

    public bool HasSelectedTile => SelectedTile is not null;

    public Bitmap? SelectedBackgroundBitmap => SelectedBackgroundOption?.Resource?.Bitmap;

    public string SelectedBackgroundDisplayName => SelectedBackgroundOption?.DisplayName ?? "<none>";

    public string SelectedInstanceObjectName => SelectedInstance?.Object?.Name ?? "<undefined>";

    public string SelectedTileBackgroundName => SelectedTile?.Background?.Name ?? "<undefined>";

    public IGmlCodeDocument? SelectedInstanceCodeDocument => _selectedInstanceCodeDocument;

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
        enableViews = room.EnableViews;
        viewClearScreen = room.ViewClearScreen;
        clearDisplayBuffer = room.ClearDisplayBuffer;

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

    partial void OnEnableViewsChanged(bool value)
    {
        _room.EnableViews = value;
        InvalidateCanvas();
    }

    partial void OnViewClearScreenChanged(bool value) => _room.ViewClearScreen = value;

    partial void OnClearDisplayBufferChanged(bool value) => _room.ClearDisplayBuffer = value;

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

    partial void OnSelectedInstanceChanged(RoomInstance? value)
    {
        _selectedInstanceCodeDocument = value is null ? null : new RoomInstanceCodeDocumentViewModel(value);
        _isSynchronizingSelectedInstanceEditor = true;
        SelectedInstanceX = value?.X ?? 0;
        SelectedInstanceY = value?.Y ?? 0;
        SelectedInstanceScaleX = value?.ScaleX ?? 1.0;
        SelectedInstanceScaleY = value?.ScaleY ?? 1.0;
        SelectedInstanceRotation = value?.Rotation ?? 0.0;
        _isSynchronizingSelectedInstanceEditor = false;
        OnPropertyChanged(nameof(SelectedInstanceCodeDocument));
        InvalidateCanvas();
    }

    partial void OnSelectedTileChanged(RoomTile? value)
    {
        _isSynchronizingSelectedTileEditor = true;
        SelectedTileX = value?.X ?? 0;
        SelectedTileY = value?.Y ?? 0;
        SelectedTileWidth = value?.Width ?? 16;
        SelectedTileHeight = value?.Height ?? 16;
        SelectedTileSourceX = value?.SourceX ?? 0;
        SelectedTileSourceY = value?.SourceY ?? 0;
        SelectedTileScaleX = value?.ScaleX ?? 1.0;
        SelectedTileScaleY = value?.ScaleY ?? 1.0;
        SelectedTileDepth = value?.Depth ?? 1000000;
        _isSynchronizingSelectedTileEditor = false;
        InvalidateCanvas();
    }

    partial void OnSelectedInstanceXChanged(int value)
    {
        if (_isSynchronizingSelectedInstanceEditor || SelectedInstance is null)
        {
            return;
        }

        SelectedInstance.X = value;
        RebuildRenderCaches();
        InvalidateCanvas();
    }

    partial void OnSelectedInstanceYChanged(int value)
    {
        if (_isSynchronizingSelectedInstanceEditor || SelectedInstance is null)
        {
            return;
        }

        SelectedInstance.Y = value;
        RebuildRenderCaches();
        InvalidateCanvas();
    }

    partial void OnSelectedInstanceScaleXChanged(double value)
    {
        if (_isSynchronizingSelectedInstanceEditor || SelectedInstance is null)
        {
            return;
        }

        SelectedInstance.ScaleX = value;
        RebuildRenderCaches();
        InvalidateCanvas();
    }

    partial void OnSelectedInstanceScaleYChanged(double value)
    {
        if (_isSynchronizingSelectedInstanceEditor || SelectedInstance is null)
        {
            return;
        }

        SelectedInstance.ScaleY = value;
        RebuildRenderCaches();
        InvalidateCanvas();
    }

    partial void OnSelectedInstanceRotationChanged(double value)
    {
        if (_isSynchronizingSelectedInstanceEditor || SelectedInstance is null)
        {
            return;
        }

        SelectedInstance.Rotation = value;
        InvalidateCanvas();
    }

    partial void OnSelectedTileXChanged(int value)
    {
        if (_isSynchronizingSelectedTileEditor || SelectedTile is null)
        {
            return;
        }

        SelectedTile.X = value;
        RebuildRenderCaches();
        InvalidateCanvas();
    }

    partial void OnSelectedTileYChanged(int value)
    {
        if (_isSynchronizingSelectedTileEditor || SelectedTile is null)
        {
            return;
        }

        SelectedTile.Y = value;
        RebuildRenderCaches();
        InvalidateCanvas();
    }

    partial void OnSelectedTileWidthChanged(int value)
    {
        if (value <= 0)
        {
            SelectedTileWidth = 1;
            return;
        }

        if (_isSynchronizingSelectedTileEditor || SelectedTile is null)
        {
            return;
        }

        SelectedTile.Width = value;
        InvalidateCanvas();
    }

    partial void OnSelectedTileHeightChanged(int value)
    {
        if (value <= 0)
        {
            SelectedTileHeight = 1;
            return;
        }

        if (_isSynchronizingSelectedTileEditor || SelectedTile is null)
        {
            return;
        }

        SelectedTile.Height = value;
        InvalidateCanvas();
    }

    partial void OnSelectedTileSourceXChanged(int value)
    {
        if (value < 0)
        {
            SelectedTileSourceX = 0;
            return;
        }

        if (_isSynchronizingSelectedTileEditor || SelectedTile is null)
        {
            return;
        }

        SelectedTile.SourceX = value;
        InvalidateCanvas();
    }

    partial void OnSelectedTileSourceYChanged(int value)
    {
        if (value < 0)
        {
            SelectedTileSourceY = 0;
            return;
        }

        if (_isSynchronizingSelectedTileEditor || SelectedTile is null)
        {
            return;
        }

        SelectedTile.SourceY = value;
        InvalidateCanvas();
    }

    partial void OnSelectedTileScaleXChanged(double value)
    {
        if (_isSynchronizingSelectedTileEditor || SelectedTile is null)
        {
            return;
        }

        SelectedTile.ScaleX = value;
        InvalidateCanvas();
    }

    partial void OnSelectedTileScaleYChanged(double value)
    {
        if (_isSynchronizingSelectedTileEditor || SelectedTile is null)
        {
            return;
        }

        SelectedTile.ScaleY = value;
        InvalidateCanvas();
    }

    partial void OnSelectedTileDepthChanged(int value)
    {
        if (_isSynchronizingSelectedTileEditor || SelectedTile is null)
        {
            return;
        }

        SelectedTile.Depth = value;
        RebuildRenderCaches();
        InvalidateCanvas();
    }

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

    public bool TrySelectTopmostAt(Point roomPoint)
    {
        return ActiveLayer switch
        {
            RoomEditLayer.Instances => TrySelectTopmostInstance(roomPoint),
            RoomEditLayer.Tiles => TrySelectTopmostTile(roomPoint),
            _ => false
        };
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

        RoomBackgroundOptions.Clear();
        RoomBackgroundOptions.Add(new ResourceReferenceOption<Background>("<none>", null));
        foreach (var background in project.Backgrounds.OrderBy(static background => background.Name, StringComparer.OrdinalIgnoreCase))
        {
            RoomBackgroundOptions.Add(new ResourceReferenceOption<Background>(background.Name, background));
        }

        ViewObjectOptions.Clear();
        ViewObjectOptions.Add(new ResourceReferenceOption<GameObject>("<none>", null));
        foreach (var gameObject in project.Objects.OrderBy(static objectItem => objectItem.Name, StringComparer.OrdinalIgnoreCase))
        {
            ViewObjectOptions.Add(new ResourceReferenceOption<GameObject>(gameObject.Name, gameObject));
        }

        RoomBackgroundSlots.Clear();
        for (var index = 0; index < _room.Backgrounds.Count; index++)
        {
            RoomBackgroundSlots.Add(new RoomBackgroundSlotViewModel(index, _room.Backgrounds[index], RoomBackgroundOptions, InvalidateCanvas));
        }

        RoomViewSlots.Clear();
        for (var index = 0; index < _room.Views.Count; index++)
        {
            RoomViewSlots.Add(new RoomViewSlotViewModel(index, _room.Views[index], ViewObjectOptions, InvalidateCanvas));
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

    internal static ResourceReferenceOption<TResource>? FindOption<TResource>(
        IEnumerable<ResourceReferenceOption<TResource>> options,
        TResource? resource) where TResource : Resource
    {
        return options.FirstOrDefault(option => ReferenceEquals(option.Resource, resource))
            ?? options.FirstOrDefault(option => option.Resource is null);
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
        SelectedInstance = roomInstance;
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
        SelectedTile = roomTile;
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
            if (ReferenceEquals(SelectedInstance, roomInstance))
            {
                SelectedInstance = null;
            }
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
            if (ReferenceEquals(SelectedTile, roomTile))
            {
                SelectedTile = null;
            }
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

    private bool TrySelectTopmostInstance(Point roomPoint)
    {
        for (var index = _orderedInstances.Count - 1; index >= 0; index--)
        {
            var roomInstance = _orderedInstances[index];
            if (!GetInstanceBounds(roomInstance).Contains(roomPoint))
            {
                continue;
            }

            SelectedInstance = roomInstance;
            return true;
        }

        return false;
    }

    private bool TrySelectTopmostTile(Point roomPoint)
    {
        for (var index = _orderedTiles.Count - 1; index >= 0; index--)
        {
            var roomTile = _orderedTiles[index];
            if (!GetTileBounds(roomTile).Contains(roomPoint))
            {
                continue;
            }

            SelectedTile = roomTile;
            return true;
        }

        return false;
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

public partial class RoomInstanceCodeDocumentViewModel : ObservableObject, IGmlCodeDocument
{
    private readonly RoomInstance _roomInstance;

    [ObservableProperty]
    private string sourceCode;

    public RoomInstanceCodeDocumentViewModel(RoomInstance roomInstance)
    {
        _roomInstance = roomInstance;
        sourceCode = roomInstance.Code;
    }

    partial void OnSourceCodeChanged(string value)
    {
        _roomInstance.Code = value;
    }
}

public partial class RoomBackgroundSlotViewModel : ObservableObject
{
    private readonly RoomBackground _roomBackground;
    private readonly Action _invalidateCanvas;

    [ObservableProperty]
    private ResourceReferenceOption<Background>? backgroundOption;

    [ObservableProperty]
    private bool visible;

    [ObservableProperty]
    private bool foreground;

    [ObservableProperty]
    private int x;

    [ObservableProperty]
    private int y;

    [ObservableProperty]
    private bool hTiled;

    [ObservableProperty]
    private bool vTiled;

    [ObservableProperty]
    private int hSpeed;

    [ObservableProperty]
    private int vSpeed;

    [ObservableProperty]
    private bool stretch;

    public int Index { get; }

    public string Header => $"Background {Index}";

    public ObservableCollection<ResourceReferenceOption<Background>> BackgroundOptions { get; }

    public RoomBackgroundSlotViewModel(
        int index,
        RoomBackground roomBackground,
        ObservableCollection<ResourceReferenceOption<Background>> backgroundOptions,
        Action invalidateCanvas)
    {
        Index = index;
        _roomBackground = roomBackground;
        BackgroundOptions = backgroundOptions;
        _invalidateCanvas = invalidateCanvas;

        backgroundOption = RoomEditorViewModel.FindOption(backgroundOptions, roomBackground.Background);
        visible = roomBackground.Visible;
        foreground = roomBackground.Foreground;
        x = roomBackground.X;
        y = roomBackground.Y;
        hTiled = roomBackground.HTiled;
        vTiled = roomBackground.VTiled;
        hSpeed = roomBackground.HSpeed;
        vSpeed = roomBackground.VSpeed;
        stretch = roomBackground.Stretch;
    }

    partial void OnBackgroundOptionChanged(ResourceReferenceOption<Background>? value)
    {
        _roomBackground.Background = value?.Resource;
        _invalidateCanvas();
    }

    partial void OnVisibleChanged(bool value)
    {
        _roomBackground.Visible = value;
        _invalidateCanvas();
    }

    partial void OnForegroundChanged(bool value)
    {
        _roomBackground.Foreground = value;
        _invalidateCanvas();
    }

    partial void OnXChanged(int value)
    {
        _roomBackground.X = value;
        _invalidateCanvas();
    }

    partial void OnYChanged(int value)
    {
        _roomBackground.Y = value;
        _invalidateCanvas();
    }

    partial void OnHTiledChanged(bool value)
    {
        _roomBackground.HTiled = value;
        _invalidateCanvas();
    }

    partial void OnVTiledChanged(bool value)
    {
        _roomBackground.VTiled = value;
        _invalidateCanvas();
    }

    partial void OnHSpeedChanged(int value) => _roomBackground.HSpeed = value;

    partial void OnVSpeedChanged(int value) => _roomBackground.VSpeed = value;

    partial void OnStretchChanged(bool value)
    {
        _roomBackground.Stretch = value;
        _invalidateCanvas();
    }
}

public partial class RoomViewSlotViewModel : ObservableObject
{
    private readonly RoomView _roomView;
    private readonly Action _invalidateCanvas;

    [ObservableProperty]
    private ResourceReferenceOption<GameObject>? followObjectOption;

    [ObservableProperty]
    private bool visible;

    [ObservableProperty]
    private int xView;

    [ObservableProperty]
    private int yView;

    [ObservableProperty]
    private int wView;

    [ObservableProperty]
    private int hView;

    [ObservableProperty]
    private int xPort;

    [ObservableProperty]
    private int yPort;

    [ObservableProperty]
    private int wPort;

    [ObservableProperty]
    private int hPort;

    [ObservableProperty]
    private int hBorder;

    [ObservableProperty]
    private int vBorder;

    [ObservableProperty]
    private int hSpeed;

    [ObservableProperty]
    private int vSpeed;

    public int Index { get; }

    public string Header => $"View {Index}";

    public ObservableCollection<ResourceReferenceOption<GameObject>> FollowObjectOptions { get; }

    public RoomViewSlotViewModel(
        int index,
        RoomView roomView,
        ObservableCollection<ResourceReferenceOption<GameObject>> followObjectOptions,
        Action invalidateCanvas)
    {
        Index = index;
        _roomView = roomView;
        FollowObjectOptions = followObjectOptions;
        _invalidateCanvas = invalidateCanvas;

        followObjectOption = RoomEditorViewModel.FindOption(followObjectOptions, roomView.FollowObject);
        visible = roomView.Visible;
        xView = roomView.XView;
        yView = roomView.YView;
        wView = roomView.WView;
        hView = roomView.HView;
        xPort = roomView.XPort;
        yPort = roomView.YPort;
        wPort = roomView.WPort;
        hPort = roomView.HPort;
        hBorder = roomView.HBorder;
        vBorder = roomView.VBorder;
        hSpeed = roomView.HSpeed;
        vSpeed = roomView.VSpeed;
    }

    partial void OnFollowObjectOptionChanged(ResourceReferenceOption<GameObject>? value)
    {
        _roomView.FollowObject = value?.Resource;
        _invalidateCanvas();
    }

    partial void OnVisibleChanged(bool value)
    {
        _roomView.Visible = value;
        _invalidateCanvas();
    }

    partial void OnXViewChanged(int value)
    {
        _roomView.XView = value;
        _invalidateCanvas();
    }

    partial void OnYViewChanged(int value)
    {
        _roomView.YView = value;
        _invalidateCanvas();
    }

    partial void OnWViewChanged(int value)
    {
        if (value <= 0)
        {
            WView = 1;
            return;
        }

        _roomView.WView = value;
        _invalidateCanvas();
    }

    partial void OnHViewChanged(int value)
    {
        if (value <= 0)
        {
            HView = 1;
            return;
        }

        _roomView.HView = value;
        _invalidateCanvas();
    }

    partial void OnXPortChanged(int value)
    {
        _roomView.XPort = value;
        _invalidateCanvas();
    }

    partial void OnYPortChanged(int value)
    {
        _roomView.YPort = value;
        _invalidateCanvas();
    }

    partial void OnWPortChanged(int value)
    {
        if (value <= 0)
        {
            WPort = 1;
            return;
        }

        _roomView.WPort = value;
        _invalidateCanvas();
    }

    partial void OnHPortChanged(int value)
    {
        if (value <= 0)
        {
            HPort = 1;
            return;
        }

        _roomView.HPort = value;
        _invalidateCanvas();
    }

    partial void OnHBorderChanged(int value)
    {
        _roomView.HBorder = value;
        _invalidateCanvas();
    }

    partial void OnVBorderChanged(int value)
    {
        _roomView.VBorder = value;
        _invalidateCanvas();
    }

    partial void OnHSpeedChanged(int value)
    {
        _roomView.HSpeed = value;
        _invalidateCanvas();
    }

    partial void OnVSpeedChanged(int value)
    {
        _roomView.VSpeed = value;
        _invalidateCanvas();
    }
}
