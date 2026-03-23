using System.Collections.Generic;

namespace AvaloniaGM.Models;

public class Room : Resource
{
    public string Caption { get; set; } = string.Empty;

    public int Width { get; set; } = 1024;

    public int Height { get; set; } = 768;

    public int VSnap { get; set; } = 32;

    public int HSnap { get; set; } = 32;

    public bool Isometric { get; set; }

    public int Speed { get; set; } = 30;

    public bool Persistent { get; set; }

    public int Colour { get; set; } = 12632256;

    public bool ShowColour { get; set; } = true;

    public string Code { get; set; } = string.Empty;

    public bool EnableViews { get; set; }

    public bool ViewClearScreen { get; set; } = true;

    public bool ClearDisplayBuffer { get; set; } = true;

    public RoomMakerSettings MakerSettings { get; set; } = new();

    public List<RoomBackground> Backgrounds { get; } = [];

    public List<RoomView> Views { get; } = [];

    public List<RoomInstance> Instances { get; } = [];

    public List<RoomTile> Tiles { get; } = [];

    public bool PhysicsWorld { get; set; }

    public int PhysicsWorldTop { get; set; }

    public int PhysicsWorldLeft { get; set; }

    public int PhysicsWorldRight { get; set; } = 1024;

    public int PhysicsWorldBottom { get; set; } = 768;

    public float PhysicsWorldGravityX { get; set; }

    public float PhysicsWorldGravityY { get; set; } = 10f;

    public float PhysicsWorldPixToMeters { get; set; } = 0.1f;

    public Room()
    {
        for (var index = 0; index < 8; index++)
        {
            Backgrounds.Add(new RoomBackground());
            Views.Add(new RoomView());
        }
    }
}

public class RoomMakerSettings
{
    public bool IsSet { get; set; }

    public int Width { get; set; }

    public int Height { get; set; }

    public bool ShowGrid { get; set; }

    public bool ShowObjects { get; set; }

    public bool ShowTiles { get; set; }

    public bool ShowBackgrounds { get; set; }

    public bool ShowForegrounds { get; set; }

    public bool ShowViews { get; set; }

    public bool DeleteUnderlyingObj { get; set; }

    public bool DeleteUnderlyingTiles { get; set; }

    public int Page { get; set; }

    public int XOffset { get; set; }

    public int YOffset { get; set; }
}

public class RoomView
{
    public bool Visible { get; set; }

    public GameObject? FollowObject { get; set; }

    public int XView { get; set; }

    public int YView { get; set; }

    public int WView { get; set; } = 1024;

    public int HView { get; set; } = 768;

    public int XPort { get; set; }

    public int YPort { get; set; }

    public int WPort { get; set; } = 1024;

    public int HPort { get; set; } = 768;

    public int HBorder { get; set; } = 32;

    public int VBorder { get; set; } = 32;

    public int HSpeed { get; set; } = -1;

    public int VSpeed { get; set; } = -1;
}

public class RoomBackground
{
    public bool Visible { get; set; }

    public bool Foreground { get; set; }

    public Background? Background { get; set; }

    public int X { get; set; }

    public int Y { get; set; }

    public bool HTiled { get; set; } = true;

    public bool VTiled { get; set; } = true;

    public int HSpeed { get; set; }

    public int VSpeed { get; set; }

    public int Blend { get; set; } = 0xFFFFFF;

    public double Alpha { get; set; } = 1.0;

    public bool Stretch { get; set; }
}

public class RoomInstance
{
    public string Name { get; set; } = string.Empty;

    public int Id { get; set; }

    public GameObject? Object { get; set; }

    public int X { get; set; }

    public int Y { get; set; }

    public string Code { get; set; } = string.Empty;

    public double ScaleX { get; set; } = 1.0;

    public double ScaleY { get; set; } = 1.0;

    public uint Colour { get; set; } = uint.MaxValue;

    public double Rotation { get; set; }
}

public class RoomTile
{
    public int Id { get; set; }

    public Background? Background { get; set; }

    public int X { get; set; }

    public int Y { get; set; }

    public int Width { get; set; }

    public int Height { get; set; }

    public int SourceX { get; set; }

    public int SourceY { get; set; }

    public int Depth { get; set; }

    public double ScaleX { get; set; } = 1.0;

    public double ScaleY { get; set; } = 1.0;

    public int Blend { get; set; } = 0xFFFFFF;

    public double Alpha { get; set; } = 1.0;
}
