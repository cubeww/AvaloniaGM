using System.Collections.Generic;

namespace AvaloniaGM.Models;

public class Project
{
    public string Name { get; set; } = string.Empty;

    public List<string> Configurations { get; } = [];

    public List<ProjectConfigurationFile> ConfigurationFiles { get; } = [];

    public List<ProjectConstant> Constants { get; } = [];

    public ProjectHelp Help { get; set; } = new();

    public ProjectTutorialState TutorialState { get; set; } = new();

    public List<ProjectResourceTreeNode> ResourceTree { get; } = [];

    public List<Sprite> Sprites { get; } = [];

    public List<Sound> Sounds { get; } = [];

    public List<Background> Backgrounds { get; } = [];

    public List<GamePath> Paths { get; } = [];

    public List<Script> Scripts { get; } = [];

    public List<Shader> Shaders { get; } = [];

    public List<Font> Fonts { get; } = [];

    public List<GameObject> Objects { get; } = [];

    public List<Timeline> Timelines { get; } = [];

    public List<Room> Rooms { get; } = [];

    public List<DataFile> DataFiles { get; } = [];

    public List<Extension> Extensions { get; } = [];
}

public class ProjectResourceTreeNode
{
    public string Name { get; set; } = string.Empty;

    public ProjectResourceKind Kind { get; set; }

    public Resource? Resource { get; set; }

    public List<ProjectResourceTreeNode> Children { get; } = [];

    public bool IsFolder => Resource is null;
}

public class ProjectConstant
{
    public string Name { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;
}

public class ProjectConfigurationFile
{
    public string FilePath { get; set; } = string.Empty;

    public byte[]? RawData { get; set; }
}

public class ProjectHelp
{
    public string RtfFileName { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;
}

public class ProjectTutorialState
{
    public bool IsTutorial { get; set; }

    public string TutorialName { get; set; } = string.Empty;

    public int TutorialPage { get; set; }
}

public enum ProjectResourceKind
{
    Unknown = 0,
    Sprite = 1,
    Sound = 2,
    Background = 3,
    Path = 4,
    Script = 5,
    Shader = 6,
    Font = 7,
    Object = 8,
    Timeline = 9,
    Room = 10,
    DataFile = 11,
    Extension = 12,
}
