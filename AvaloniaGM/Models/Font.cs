using System.Collections.Generic;
using Avalonia.Media.Imaging;

namespace AvaloniaGM.Models;

public class Font : Resource
{
    public string FontName { get; set; } = "Arial";

    public float Size { get; set; } = 12f;

    public bool Bold { get; set; }

    public bool Italic { get; set; }

    public int First { get; set; } = 32;

    public int Last { get; set; } = 127;

    public int CharSet { get; set; } = 1;

    public int AntiAlias { get; set; } = 3;

    public List<FontGlyph> Glyphs { get; } = [];

    public Bitmap? Bitmap { get; set; }

    public List<int> TextureGroups { get; } = [];

    public List<FontRange> Ranges { get; } = [];

    public Font()
    {
        TextureGroups.Add(0);
        Ranges.Add(new FontRange
        {
            Start = 32,
            End = 127,
        });
    }
}

public class FontGlyph
{
    public int Character { get; set; }

    public int X { get; set; }

    public int Y { get; set; }

    public int Width { get; set; }

    public int Height { get; set; }

    public int Shift { get; set; }

    public int Offset { get; set; }

    public List<FontKerning> Kerning { get; } = [];
}

public class FontKerning
{
    public int Other { get; set; }

    public int Amount { get; set; }
}

public class FontRange
{
    public int Start { get; set; }

    public int End { get; set; }
}
