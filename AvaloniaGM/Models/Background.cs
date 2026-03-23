using System.Collections.Generic;
using Avalonia.Media.Imaging;

namespace AvaloniaGM.Models;

public class Background : Resource
{
    public bool IsTileset { get; set; }

    public Bitmap? Bitmap { get; set; }

    public int Width { get; set; }

    public int Height { get; set; }

    public bool HTile { get; set; } = true;

    public bool VTile { get; set; } = true;

    public int TileWidth { get; set; } = 16;

    public int TileHeight { get; set; } = 16;

    public int TileXOffset { get; set; }

    public int TileYOffset { get; set; }

    public int TileHorizontalSeparation { get; set; }

    public int TileVerticalSeparation { get; set; }

    public List<int> TextureGroups { get; } = [];

    public bool For3D { get; set; }

    public bool DynamicTexturePage { get; set; }

    public Background()
    {
        TextureGroups.Add(0);
    }
}
