using System.Collections.Generic;

namespace AvaloniaGM.Models;

public class Timeline : Resource
{
    public List<TimelineMoment> Moments { get; } = [];
}

public class TimelineMoment
{
    public int Step { get; set; }

    public List<GameObjectAction> Actions { get; } = [];
}
