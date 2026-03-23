using System.Collections.Generic;

namespace AvaloniaGM.Models;

public class GamePath : Resource
{
    public int Kind { get; set; }

    public bool Closed { get; set; } = true;

    public int Precision { get; set; } = 4;

    public List<GamePathPoint> Points { get; } = [];

    public int BackRoom { get; set; } = -1;

    public int HSnap { get; set; } = 16;

    public int VSnap { get; set; } = 16;
}

public class GamePathPoint
{
    public double X { get; set; }

    public double Y { get; set; }

    public double Speed { get; set; }
}
