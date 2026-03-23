using System.Collections.Generic;

namespace AvaloniaGM.Models;

public class GamePath : Resource
{
    public int Kind { get; set; }

    public bool Closed { get; set; }

    public int Precision { get; set; }

    public List<GamePathPoint> Points { get; } = [];

    public int BackRoom { get; set; }

    public int HSnap { get; set; }

    public int VSnap { get; set; }
}

public class GamePathPoint
{
    public double X { get; set; }

    public double Y { get; set; }

    public double Speed { get; set; }
}
