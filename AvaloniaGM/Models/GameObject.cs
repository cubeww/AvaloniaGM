using System.Collections.Generic;

namespace AvaloniaGM.Models;

public class GameObject : Resource
{
    public Sprite? Sprite { get; set; }

    public bool Solid { get; set; }

    public bool Visible { get; set; }

    public int Depth { get; set; }

    public bool Persistent { get; set; }

    public GameObject? Parent { get; set; }

    public Sprite? Mask { get; set; }

    public List<GameObjectEvent> Events { get; } = [];

    public bool PhysicsObject { get; set; }

    public bool PhysicsObjectSensor { get; set; }

    public int PhysicsObjectShape { get; set; }

    public float PhysicsObjectDensity { get; set; }

    public float PhysicsObjectRestitution { get; set; }

    public int PhysicsObjectGroup { get; set; }

    public float PhysicsObjectLinearDamping { get; set; }

    public float PhysicsObjectAngularDamping { get; set; }

    public float PhysicsObjectFriction { get; set; }

    public bool PhysicsObjectAwake { get; set; }

    public bool PhysicsObjectKinematic { get; set; }

    public List<GameObjectPhysicsShapePoint> PhysicsShapePoints { get; } = [];
}

public class GameObjectEvent
{
    public GameObjectEventType EventType { get; set; }

    public int EventNumber { get; set; }

    public GameObject? CollisionObject { get; set; }

    public List<GameObjectAction> Actions { get; } = [];
}

public class GameObjectAction
{
    public int LibId { get; set; }

    public int Id { get; set; }

    public GameObjectActionKind Kind { get; set; }

    public bool UseRelative { get; set; }

    public bool IsQuestion { get; set; }

    public bool UseApplyTo { get; set; }

    public GameObjectActionExecuteType ExecuteType { get; set; }

    public string FunctionName { get; set; } = string.Empty;

    public string CodeString { get; set; } = string.Empty;

    public string WhoName { get; set; } = string.Empty;

    public bool Relative { get; set; }

    public bool IsNot { get; set; }

    public List<GameObjectActionArgument> Arguments { get; } = [];
}

public class GameObjectActionArgument
{
    public GameObjectActionArgumentKind Kind { get; set; }

    public string Value { get; set; } = string.Empty;
}

public class GameObjectPhysicsShapePoint
{
    public float X { get; set; }

    public float Y { get; set; }
}

public enum GameObjectEventType
{
    Create = 0,
    Destroy = 1,
    Alarm = 2,
    Step = 3,
    Collision = 4,
    Keyboard = 5,
    Mouse = 6,
    Other = 7,
    Draw = 8,
    KeyPress = 9,
    KeyRelease = 10,
    Trigger = 11,
    CleanUp = 12,
    Gesture = 13,
}

public enum GameObjectActionKind
{
    Normal = 0,
    Begin = 1,
    End = 2,
    Else = 3,
    Exit = 4,
    Repeat = 5,
    Variable = 6,
    Code = 7,
}

public enum GameObjectActionExecuteType
{
    Nothing = 0,
    Function = 1,
    Code = 2,
}

public enum GameObjectActionArgumentKind
{
    Constant = -1,
    Expression = 0,
    String = 1,
    StringExpression = 2,
    Boolean = 3,
    Menu = 4,
    Sprite = 5,
    Sound = 6,
    Background = 7,
    Path = 8,
    Script = 9,
    Object = 10,
    Room = 11,
    FontReference = 12,
    Color = 13,
    Timeline = 14,
    Font = 15,
}
