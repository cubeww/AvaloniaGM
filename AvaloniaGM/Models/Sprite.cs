using System.Collections.Generic;
using Avalonia.Media.Imaging;

namespace AvaloniaGM.Models;

public class Sprite : Resource
{
    public const float DefaultSwfPrecision = 0.5f;

    public SpriteType Type { get; set; } = SpriteType.Bitmap;

    public int XOrigin { get; set; }

    public int YOrigin { get; set; }

    public SpriteCollisionKind CollisionKind { get; set; } = SpriteCollisionKind.Rectangle;

    public int CollisionTolerance { get; set; }

    public bool SeparateCollisionMasks { get; set; }

    public SpriteBoundingBoxMode BoundingBoxMode { get; set; } = SpriteBoundingBoxMode.Automatic;

    public int BoundingBoxLeft { get; set; }

    public int BoundingBoxRight { get; set; }

    public int BoundingBoxTop { get; set; }

    public int BoundingBoxBottom { get; set; }

    public bool HTile { get; set; }

    public bool VTile { get; set; }

    public List<int> TextureGroups { get; } = [];

    public bool For3D { get; set; }

    public bool DynamicTexturePage { get; set; }

    public int Width { get; set; } = 32;

    public int Height { get; set; } = 32;

    public List<SpriteFrame> Frames { get; } = [];

    public string? SwfFile { get; set; }

    public float SwfPrecision { get; set; } = DefaultSwfPrecision;

    public string? SpineFile { get; set; }

    public Sprite()
    {
        TextureGroups.Add(0);
    }
}

public class SpriteFrame
{
    public int Index { get; set; }

    public Bitmap? Bitmap { get; set; }

    public int Width => Bitmap?.PixelSize.Width ?? 0;

    public int Height => Bitmap?.PixelSize.Height ?? 0;
}

public enum SpriteType
{
    Bitmap = 0,
    Swf = 1,
    Spine = 2,
    Vector = 3,
}

public enum SpriteCollisionKind
{
    Precise = 0,
    Rectangle = 1,
    Ellipse = 2,
    Diamond = 3,
    PrecisePerFrame = 4,
    RotatedRectangle = 5,
    SpineMesh = 6,
}

public enum SpriteBoundingBoxMode
{
    Automatic = 0,
    FullImage = 1,
    Manual = 2,
}
