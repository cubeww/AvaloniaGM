using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using AvaloniaGM.Models;

namespace AvaloniaGM.Services;

public partial class ProjectGmxSerializer
{
    private readonly Dictionary<string, Sprite> _spritesByName = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Background> _backgroundsByName = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, GameObject> _objectsByName = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<(GameObject Object, string SpriteName)> _pendingSpriteReferences = [];
    private readonly List<(GameObject Object, string MaskName)> _pendingMaskReferences = [];
    private readonly List<(GameObject Object, string ParentName)> _pendingParentReferences = [];
    private readonly List<(GameObjectEvent Event, string CollisionObjectName)> _pendingCollisionObjectReferences = [];
    private readonly List<(RoomView View, string ObjectName)> _pendingRoomViewObjectReferences = [];
    private readonly List<(RoomInstance Instance, string ObjectName)> _pendingRoomInstanceObjectReferences = [];
    private readonly List<(RoomBackground Background, string BackgroundName)> _pendingRoomBackgroundReferences = [];
    private readonly List<(RoomTile Tile, string BackgroundName)> _pendingRoomTileBackgroundReferences = [];
    private int _nextRoomInstanceId = 100000;
    private int _nextRoomTileId = 10000000;

    public Project DeserializeProject(string projectGmxPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectGmxPath);

        ResetParsingState();

        var fullPath = Path.GetFullPath(projectGmxPath);
        var document = XDocument.Load(fullPath, LoadOptions.None);
        var root = document.Root;

        if (root is null || root.Name != "assets")
        {
            throw new InvalidDataException($"File '{fullPath}' is not a valid project GMX file.");
        }

        var projectDirectory = Path.GetDirectoryName(fullPath)
            ?? throw new InvalidDataException($"Unable to determine directory for project file '{fullPath}'.");

        var project = new Project
        {
            Name = GetResourceNameFromPath(fullPath, ".project.gmx"),
        };

        ReadProjectConfigurations(root.Element("Configs"), project.Configurations);
        ReadProjectConfigurationFiles(projectDirectory, project.Configurations, project.ConfigurationFiles);
        ReadProjectConstants(root.Element("constants"), project.Constants);
        ReadProjectHelp(root.Element("help"), project.Help, projectDirectory);
        ReadProjectTutorialState(root.Element("TutorialState"), project.TutorialState);

        var extensionMap = LoadProjectFlatResources(
            root.Element("NewExtensions"),
            "extension",
            relativePath => DeserializeExtension(ResolveProjectResourcePath(projectDirectory, relativePath, ".extension.gmx")),
            project.Extensions);

        project.DataFiles.AddRange(DeserializeDataFiles(fullPath));
        var dataFileMap = project.DataFiles.ToDictionary(
            static dataFile => NormalizeProjectPath(dataFile.FileName),
            static dataFile => dataFile,
            StringComparer.OrdinalIgnoreCase);

        var soundMap = LoadProjectGroupedResources(
            root.Element("sounds"),
            "sounds",
            "sound",
            relativePath => DeserializeSound(ResolveProjectResourcePath(projectDirectory, relativePath, ".sound.gmx")),
            project.Sounds);

        var spriteMap = LoadProjectGroupedResources(
            root.Element("sprites"),
            "sprites",
            "sprite",
            relativePath => DeserializeSprite(ResolveProjectResourcePath(projectDirectory, relativePath, ".sprite.gmx")),
            project.Sprites);

        var backgroundMap = LoadProjectGroupedResources(
            root.Element("backgrounds"),
            "backgrounds",
            "background",
            relativePath => DeserializeBackground(ResolveProjectResourcePath(projectDirectory, relativePath, ".background.gmx")),
            project.Backgrounds);

        var pathMap = LoadProjectGroupedResources(
            root.Element("paths"),
            "paths",
            "path",
            relativePath => DeserializePath(ResolveProjectResourcePath(projectDirectory, relativePath, ".path.gmx")),
            project.Paths);

        var scriptMap = LoadProjectGroupedResources(
            root.Element("scripts"),
            "scripts",
            "script",
            relativePath => DeserializeScript(ResolveProjectResourcePath(projectDirectory, relativePath)),
            project.Scripts);

        var shaderMap = LoadProjectGroupedResources(
            root.Element("shaders"),
            "shaders",
            "shader",
            relativePath => DeserializeShader(ResolveProjectResourcePath(projectDirectory, relativePath)),
            project.Shaders,
            static (element, shader) =>
            {
                shader.ProjectType = ReadOptionalAttributeString(element, "type") ?? string.Empty;
            });

        var fontMap = LoadProjectGroupedResources(
            root.Element("fonts"),
            "fonts",
            "font",
            relativePath => DeserializeFont(ResolveProjectResourcePath(projectDirectory, relativePath, ".font.gmx")),
            project.Fonts);

        var timelineMap = LoadProjectGroupedResources(
            root.Element("timelines"),
            "timelines",
            "timeline",
            relativePath => DeserializeTimeline(ResolveProjectResourcePath(projectDirectory, relativePath, ".timeline.gmx")),
            project.Timelines);

        var objectMap = LoadProjectGroupedResources(
            root.Element("objects"),
            "objects",
            "object",
            relativePath => DeserializeGameObject(ResolveProjectResourcePath(projectDirectory, relativePath, ".object.gmx")),
            project.Objects);

        var roomMap = LoadProjectGroupedResources(
            root.Element("rooms"),
            "rooms",
            "room",
            relativePath => DeserializeRoom(ResolveProjectResourcePath(projectDirectory, relativePath, ".room.gmx")),
            project.Rooms);

        AddProjectResourceTreeNode(
            project.ResourceTree,
            root.Element("datafiles"),
            element => BuildProjectDataFilesTree(
                element,
                dataFileMap,
                NormalizeProjectPath(ReadOptionalAttributeString(element, "name") ?? "datafiles")));
        AddProjectResourceTreeNode(project.ResourceTree, root.Element("NewExtensions"), element => BuildProjectFlatResourceTree(element, ProjectResourceKind.Extension, "extension", extensionMap, "extensions"));
        AddProjectResourceTreeNode(project.ResourceTree, root.Element("sounds"), element => BuildProjectGroupedResourceTree(element, "sounds", "sound", ProjectResourceKind.Sound, soundMap));
        AddProjectResourceTreeNode(project.ResourceTree, root.Element("sprites"), element => BuildProjectGroupedResourceTree(element, "sprites", "sprite", ProjectResourceKind.Sprite, spriteMap));
        AddProjectResourceTreeNode(project.ResourceTree, root.Element("backgrounds"), element => BuildProjectGroupedResourceTree(element, "backgrounds", "background", ProjectResourceKind.Background, backgroundMap));
        AddProjectResourceTreeNode(project.ResourceTree, root.Element("paths"), element => BuildProjectGroupedResourceTree(element, "paths", "path", ProjectResourceKind.Path, pathMap));
        AddProjectResourceTreeNode(project.ResourceTree, root.Element("scripts"), element => BuildProjectGroupedResourceTree(element, "scripts", "script", ProjectResourceKind.Script, scriptMap));
        AddProjectResourceTreeNode(project.ResourceTree, root.Element("shaders"), element => BuildProjectGroupedResourceTree(element, "shaders", "shader", ProjectResourceKind.Shader, shaderMap));
        AddProjectResourceTreeNode(project.ResourceTree, root.Element("fonts"), element => BuildProjectGroupedResourceTree(element, "fonts", "font", ProjectResourceKind.Font, fontMap));
        AddProjectResourceTreeNode(project.ResourceTree, root.Element("objects"), element => BuildProjectGroupedResourceTree(element, "objects", "object", ProjectResourceKind.Object, objectMap));
        AddProjectResourceTreeNode(project.ResourceTree, root.Element("timelines"), element => BuildProjectGroupedResourceTree(element, "timelines", "timeline", ProjectResourceKind.Timeline, timelineMap));
        AddProjectResourceTreeNode(project.ResourceTree, root.Element("rooms"), element => BuildProjectGroupedResourceTree(element, "rooms", "room", ProjectResourceKind.Room, roomMap));

        return project;
    }

    public Shader DeserializeShader(string shaderFilePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(shaderFilePath);

        var fullPath = Path.GetFullPath(shaderFilePath);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException($"Shader file '{fullPath}' was not found.", fullPath);
        }

        var sourceCode = File.ReadAllText(fullPath);
        var splitIndex = sourceCode.IndexOf(Shader.SplitMarker, StringComparison.Ordinal);

        if (splitIndex < 0)
        {
            return new Shader
            {
                Name = Path.GetFileNameWithoutExtension(fullPath),
                VertexSource = sourceCode,
            };
        }

        var markerEndIndex = splitIndex + Shader.SplitMarker.Length;
        var fragmentStartIndex = markerEndIndex;

        while (fragmentStartIndex < sourceCode.Length
            && (sourceCode[fragmentStartIndex] == '\r'
                || sourceCode[fragmentStartIndex] == '\n'))
        {
            fragmentStartIndex++;
        }

        return new Shader
        {
            Name = Path.GetFileNameWithoutExtension(fullPath),
            VertexSource = sourceCode[..splitIndex].TrimEnd('\r', '\n'),
            FragmentSource = sourceCode[fragmentStartIndex..],
        };
    }

    public Script DeserializeScript(string scriptFilePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scriptFilePath);

        var fullPath = Path.GetFullPath(scriptFilePath);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException($"Script file '{fullPath}' was not found.", fullPath);
        }

        return new Script
        {
            Name = Path.GetFileNameWithoutExtension(fullPath),
            SourceCode = File.ReadAllText(fullPath),
        };
    }

    public Extension DeserializeExtension(string extensionGmxPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(extensionGmxPath);

        var fullPath = Path.GetFullPath(extensionGmxPath);
        var document = XDocument.Load(fullPath, LoadOptions.None);
        var root = document.Root;

        if (root is null || root.Name != "extension")
        {
            throw new InvalidDataException($"File '{fullPath}' is not a valid extension GMX file.");
        }

        var extension = new Extension
        {
            Name = GetResourceNameFromPath(fullPath, ".extension.gmx"),
        };

        foreach (var element in root.Elements())
        {
            switch (element.Name.LocalName)
            {
                case "name":
                    extension.Name = ReadText(element);
                    break;
                case "version":
                    extension.Version = ReadText(element);
                    break;
                case "author":
                    extension.Author = ReadText(element);
                    break;
                case "date":
                    extension.Date = ReadText(element);
                    break;
                case "license":
                    extension.License = ReadText(element);
                    break;
                case "description":
                    extension.Description = ReadText(element);
                    break;
                case "helpfile":
                    extension.HelpFile = ReadText(element);
                    break;
                case "installdir":
                    extension.InstallDir = ReadText(element);
                    break;
                case "classname":
                    extension.ClassName = ReadText(element);
                    break;
                case "androidclassname":
                    extension.AndroidClassName = ReadText(element);
                    break;
                case "macsourcedir":
                    extension.MacSourceDir = ReadText(element);
                    break;
                case "maclinkerflags":
                    extension.MacLinkerFlags = ReadText(element);
                    break;
                case "maccompilerflags":
                    extension.MacCompilerFlags = ReadText(element);
                    break;
                case "iosSystemFrameworks":
                    ReadExtensionFrameworks(element, extension.IOSSystemFrameworks);
                    break;
                case "iosThirdPartyFrameworks":
                    ReadExtensionFrameworks(element, extension.IOSThirdPartyFrameworks);
                    break;
                case "IncludedResources":
                    ReadExtensionIncludedResources(element, fullPath, extension.IncludedResources);
                    break;
                case "files":
                    ReadExtensionIncludes(element, fullPath, extension.Includes);
                    break;
                case "ConfigOptions":
                    ReadConfigOptions(element, extension.ConfigOptions);
                    break;
                case "ProductID":
                    extension.ProductId = ReadText(element);
                    break;
                case "packageID":
                    extension.PackageId = ReadText(element);
                    break;
            }
        }

        return extension;
    }

    public List<DataFile> DeserializeDataFiles(string projectGmxPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectGmxPath);

        var fullPath = Path.GetFullPath(projectGmxPath);
        var document = XDocument.Load(fullPath, LoadOptions.None);
        var root = document.Root;

        if (root is null || root.Name != "assets")
        {
            throw new InvalidDataException($"File '{fullPath}' is not a valid project GMX file.");
        }

        var projectDirectory = Path.GetDirectoryName(fullPath)
            ?? throw new InvalidDataException($"Unable to determine directory for project file '{fullPath}'.");

        var dataFiles = new List<DataFile>();

        foreach (var datafilesElement in root.Elements("datafiles"))
        {
            var rootRelativeDirectory = ReadOptionalAttributeString(datafilesElement, "name") ?? "datafiles";
            ReadDataFilesTree(datafilesElement, rootRelativeDirectory, projectDirectory, dataFiles);
        }

        return dataFiles;
    }

    public Timeline DeserializeTimeline(string timelineGmxPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(timelineGmxPath);

        var fullPath = Path.GetFullPath(timelineGmxPath);
        var document = XDocument.Load(fullPath, LoadOptions.None);
        var root = document.Root;

        if (root is null || root.Name != "timeline")
        {
            throw new InvalidDataException($"File '{fullPath}' is not a valid timeline GMX file.");
        }

        var timeline = new Timeline
        {
            Name = GetResourceNameFromPath(fullPath, ".timeline.gmx"),
        };

        ReadTimelineMoments(root, timeline.Moments);
        return timeline;
    }

    public Room DeserializeRoom(string roomGmxPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(roomGmxPath);

        var fullPath = Path.GetFullPath(roomGmxPath);
        var document = XDocument.Load(fullPath, LoadOptions.None);
        var root = document.Root;

        if (root is null || root.Name != "room")
        {
            throw new InvalidDataException($"File '{fullPath}' is not a valid room GMX file.");
        }

        var room = new Room
        {
            Name = GetResourceNameFromPath(fullPath, ".room.gmx"),
        };

        foreach (var element in root.Elements())
        {
            switch (element.Name.LocalName)
            {
                case "caption":
                    room.Caption = ReadText(element);
                    break;
                case "width":
                    room.Width = ReadInt32(element);
                    break;
                case "height":
                    room.Height = ReadInt32(element);
                    break;
                case "vsnap":
                    room.VSnap = ReadInt32(element);
                    break;
                case "hsnap":
                    room.HSnap = ReadInt32(element);
                    break;
                case "isometric":
                    room.Isometric = ReadGameMakerBoolean(element);
                    break;
                case "speed":
                    room.Speed = ReadInt32(element);
                    break;
                case "persistent":
                    room.Persistent = ReadGameMakerBoolean(element);
                    break;
                case "colour":
                    room.Colour = ReadInt32(element);
                    break;
                case "showcolour":
                    room.ShowColour = ReadGameMakerBoolean(element);
                    break;
                case "code":
                    room.Code = ReadText(element);
                    break;
                case "enableViews":
                    room.EnableViews = ReadGameMakerBoolean(element);
                    break;
                case "clearViewBackground":
                    room.ViewClearScreen = ReadGameMakerBoolean(element);
                    break;
                case "clearDisplayBuffer":
                    room.ClearDisplayBuffer = ReadGameMakerBoolean(element);
                    break;
                case "makerSettings":
                    ReadRoomMakerSettings(element, room.MakerSettings);
                    break;
                case "backgrounds":
                    ReadRoomBackgrounds(element, room.Backgrounds);
                    break;
                case "views":
                    ReadRoomViews(element, room.Views);
                    break;
                case "instances":
                    ReadRoomInstances(element, room.Instances);
                    break;
                case "tiles":
                    ReadRoomTiles(element, room.Tiles);
                    break;
                case "PhysicsWorld":
                    room.PhysicsWorld = ReadGameMakerBoolean(element);
                    break;
                case "PhysicsWorldTop":
                    room.PhysicsWorldTop = ReadInt32(element);
                    break;
                case "PhysicsWorldLeft":
                    room.PhysicsWorldLeft = ReadInt32(element);
                    break;
                case "PhysicsWorldRight":
                    room.PhysicsWorldRight = ReadInt32(element);
                    break;
                case "PhysicsWorldBottom":
                    room.PhysicsWorldBottom = ReadInt32(element);
                    break;
                case "PhysicsWorldGravityX":
                    room.PhysicsWorldGravityX = ReadSingle(element);
                    break;
                case "PhysicsWorldGravityY":
                    room.PhysicsWorldGravityY = ReadSingle(element);
                    break;
                case "PhysicsWorldPixToMeters":
                    room.PhysicsWorldPixToMeters = ReadSingle(element);
                    break;
            }
        }

        return room;
    }

    public GameObject DeserializeGameObject(string objectGmxPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(objectGmxPath);

        var fullPath = Path.GetFullPath(objectGmxPath);
        var document = XDocument.Load(fullPath, LoadOptions.None);
        var root = document.Root;

        if (root is null || root.Name != "object")
        {
            throw new InvalidDataException($"File '{fullPath}' is not a valid object GMX file.");
        }

        var gameObject = new GameObject
        {
            Name = GetResourceNameFromPath(fullPath, ".object.gmx"),
        };

        foreach (var element in root.Elements())
        {
            switch (element.Name.LocalName)
            {
                case "spriteName":
                    AssignSpriteReference(gameObject, ReadText(element));
                    break;
                case "solid":
                    gameObject.Solid = ReadGameMakerBoolean(element);
                    break;
                case "visible":
                    gameObject.Visible = ReadGameMakerBoolean(element);
                    break;
                case "depth":
                    gameObject.Depth = ReadInt32(element);
                    break;
                case "persistent":
                    gameObject.Persistent = ReadGameMakerBoolean(element);
                    break;
                case "parentName":
                    AssignParentReference(gameObject, ReadText(element));
                    break;
                case "maskName":
                    AssignMaskReference(gameObject, ReadText(element));
                    break;
                case "events":
                    ReadGameObjectEvents(element, gameObject.Events);
                    break;
                case "PhysicsObject":
                    gameObject.PhysicsObject = ReadGameMakerBoolean(element);
                    break;
                case "PhysicsObjectSensor":
                    gameObject.PhysicsObjectSensor = ReadGameMakerBoolean(element);
                    break;
                case "PhysicsObjectShape":
                    gameObject.PhysicsObjectShape = ReadInt32(element);
                    break;
                case "PhysicsObjectDensity":
                    gameObject.PhysicsObjectDensity = ReadSingle(element);
                    break;
                case "PhysicsObjectRestitution":
                    gameObject.PhysicsObjectRestitution = ReadSingle(element);
                    break;
                case "PhysicsObjectGroup":
                    gameObject.PhysicsObjectGroup = ReadInt32(element);
                    break;
                case "PhysicsObjectLinearDamping":
                    gameObject.PhysicsObjectLinearDamping = ReadSingle(element);
                    break;
                case "PhysicsObjectAngularDamping":
                    gameObject.PhysicsObjectAngularDamping = ReadSingle(element);
                    break;
                case "PhysicsObjectFriction":
                    gameObject.PhysicsObjectFriction = ReadSingle(element);
                    break;
                case "PhysicsObjectAwake":
                    gameObject.PhysicsObjectAwake = ReadGameMakerBoolean(element);
                    break;
                case "PhysicsObjectKinematic":
                    gameObject.PhysicsObjectKinematic = ReadGameMakerBoolean(element);
                    break;
                case "PhysicsShapePoints":
                    ReadPhysicsShapePoints(element, gameObject.PhysicsShapePoints);
                    break;
            }
        }

        RegisterGameObject(gameObject);
        return gameObject;
    }

    public Font DeserializeFont(string fontGmxPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fontGmxPath);

        var fullPath = Path.GetFullPath(fontGmxPath);
        var document = XDocument.Load(fullPath, LoadOptions.None);
        var root = document.Root;

        if (root is null || root.Name != "font")
        {
            throw new InvalidDataException($"File '{fullPath}' is not a valid font GMX file.");
        }

        var font = new Font
        {
            Name = GetResourceNameFromPath(fullPath, ".font.gmx"),
        };

        foreach (var element in root.Elements())
        {
            switch (element.Name.LocalName)
            {
                case "name":
                    font.FontName = ReadText(element);
                    break;
                case "size":
                    font.Size = float.Parse(element.Value, CultureInfo.InvariantCulture);
                    break;
                case "bold":
                    font.Bold = ReadGameMakerBoolean(element);
                    break;
                case "italic":
                    font.Italic = ReadGameMakerBoolean(element);
                    break;
                case "charset":
                    font.CharSet = ReadInt32(element);
                    break;
                case "aa":
                    font.AntiAlias = ReadInt32(element);
                    break;
                case "texgroup":
                    font.TextureGroups.Clear();
                    font.TextureGroups.Add(ReadInt32(element));
                    break;
                case "texgroups":
                    ReadIndexedGroups(element, font.TextureGroups, "texgroup");
                    break;
                case "ranges":
                    ReadFontRanges(element, font.Ranges);
                    break;
                case "glyphs":
                    ReadFontGlyphs(element, font.Glyphs);
                    break;
                case "kerningPairs":
                    ReadFontKerningPairs(element, font.Glyphs);
                    break;
                case "image":
                    font.Bitmap = ReadFontBitmap(fullPath);
                    break;
            }
        }

        font.Glyphs.Sort(static (left, right) => left.Character.CompareTo(right.Character));

        if (font.Glyphs.Count > 0)
        {
            font.First = font.Glyphs[0].Character;
            font.Last = font.Glyphs[^1].Character;
        }

        return font;
    }

    public GamePath DeserializePath(string pathGmxPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pathGmxPath);

        var fullPath = Path.GetFullPath(pathGmxPath);
        var document = XDocument.Load(fullPath, LoadOptions.None);
        var root = document.Root;

        if (root is null || root.Name != "path")
        {
            throw new InvalidDataException($"File '{fullPath}' is not a valid path GMX file.");
        }

        var gamePath = new GamePath
        {
            Name = GetResourceNameFromPath(fullPath, ".path.gmx"),
        };

        foreach (var element in root.Elements())
        {
            switch (element.Name.LocalName)
            {
                case "kind":
                    gamePath.Kind = ReadInt32(element);
                    break;
                case "closed":
                    gamePath.Closed = ReadGameMakerBoolean(element);
                    break;
                case "precision":
                    gamePath.Precision = ReadInt32(element);
                    break;
                case "backroom":
                    gamePath.BackRoom = ReadInt32(element);
                    break;
                case "hsnap":
                    gamePath.HSnap = ReadInt32(element);
                    break;
                case "vsnap":
                    gamePath.VSnap = ReadInt32(element);
                    break;
                case "points":
                    ReadPathPoints(element, gamePath.Points);
                    break;
            }
        }

        return gamePath;
    }

    public Sound DeserializeSound(string soundGmxPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(soundGmxPath);

        var fullPath = Path.GetFullPath(soundGmxPath);
        var document = XDocument.Load(fullPath, LoadOptions.None);
        var root = document.Root;

        if (root is null || root.Name != "sound")
        {
            throw new InvalidDataException($"File '{fullPath}' is not a valid sound GMX file.");
        }

        var sound = new Sound
        {
            Name = GetResourceNameFromPath(fullPath, ".sound.gmx"),
        };

        string? originalName = null;
        string? dataFileName = null;

        foreach (var element in root.Elements())
        {
            switch (element.Name.LocalName)
            {
                case "kind":
                    sound.Kind = ReadInt32(element);
                    break;
                case "extension":
                    sound.Extension = ReadText(element);
                    break;
                case "origname":
                    originalName = ReadText(element);
                    sound.OriginalName = originalName;
                    break;
                case "effects":
                    sound.Effects = ReadInt32(element);
                    break;
                case "volume":
                    sound.Volume = ReadConfiguredDouble(element);
                    break;
                case "pan":
                    sound.Pan = ReadDouble(element);
                    break;
                case "bitRates":
                    sound.CompressionQuality = MapCompressionQuality(ReadConfiguredInt32(element));
                    break;
                case "sampleRates":
                    sound.SampleRate = ReadConfiguredInt32(element);
                    break;
                case "bitDepths":
                    sound.BitDepth = ReadConfiguredInt32(element);
                    break;
                case "types":
                    sound.Stereo = ReadConfiguredInt32(element) == 1;
                    break;
                case "preload":
                    sound.Preload = ReadGameMakerBoolean(element);
                    break;
                case "data":
                    dataFileName = ReadText(element);
                    break;
                case "compressed":
                    sound.Compressed = ReadGameMakerBoolean(element);
                    break;
                case "streamed":
                    sound.Streamed = ReadGameMakerBoolean(element);
                    break;
                case "uncompressOnLoad":
                    sound.UncompressOnLoad = ReadGameMakerBoolean(element);
                    break;
                case "audioGroup":
                    sound.AudioGroup = ReadInt32(element);
                    break;
                case "exportDir":
                    sound.ExportDirectory = ReadText(element);
                    break;
            }
        }

        var audioFilePath = ResolveSoundFilePath(fullPath, originalName, dataFileName);
        if (audioFilePath is not null && File.Exists(audioFilePath))
        {
            sound.RawData = File.ReadAllBytes(audioFilePath);
        }

        return sound;
    }

    public Background DeserializeBackground(string backgroundGmxPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(backgroundGmxPath);

        var fullPath = Path.GetFullPath(backgroundGmxPath);
        var document = XDocument.Load(fullPath, LoadOptions.None);
        var root = document.Root;

        if (root is null || root.Name != "background")
        {
            throw new InvalidDataException($"File '{fullPath}' is not a valid background GMX file.");
        }

        var background = new Background
        {
            Name = GetResourceNameFromPath(fullPath, ".background.gmx"),
        };

        foreach (var element in root.Elements())
        {
            switch (element.Name.LocalName)
            {
                case "istileset":
                    background.IsTileset = ReadGameMakerBoolean(element);
                    break;
                case "tilewidth":
                    background.TileWidth = ReadInt32(element);
                    break;
                case "tileheight":
                    background.TileHeight = ReadInt32(element);
                    break;
                case "tilexoff":
                    background.TileXOffset = ReadInt32(element);
                    break;
                case "tileyoff":
                    background.TileYOffset = ReadInt32(element);
                    break;
                case "tilehsep":
                    background.TileHorizontalSeparation = ReadInt32(element);
                    break;
                case "tilevsep":
                    background.TileVerticalSeparation = ReadInt32(element);
                    break;
                case "HTile":
                    background.HTile = ReadGameMakerBoolean(element);
                    break;
                case "VTile":
                    background.VTile = ReadGameMakerBoolean(element);
                    break;
                case "TextureGroup":
                    background.TextureGroups.Clear();
                    background.TextureGroups.Add(ReadInt32(element));
                    break;
                case "TextureGroups":
                    ReadTextureGroups(element, background.TextureGroups);
                    break;
                case "For3D":
                    background.For3D = ReadGameMakerBoolean(element);
                    break;
                case "DynamicTexturePage":
                    background.DynamicTexturePage = ReadGameMakerBoolean(element);
                    break;
                case "width":
                    background.Width = ReadInt32(element);
                    break;
                case "height":
                    background.Height = ReadInt32(element);
                    break;
                case "data":
                    background.Bitmap = ReadBitmap(fullPath, ReadRelativePath(element));
                    break;
            }
        }

        if (background.Bitmap is not null)
        {
            if (background.Width == 0)
            {
                background.Width = background.Bitmap.PixelSize.Width;
            }

            if (background.Height == 0)
            {
                background.Height = background.Bitmap.PixelSize.Height;
            }
        }

        RegisterBackground(background);
        return background;
    }

    public Sprite DeserializeSprite(string spriteGmxPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(spriteGmxPath);

        var fullPath = Path.GetFullPath(spriteGmxPath);
        var document = XDocument.Load(fullPath, LoadOptions.None);
        var root = document.Root;

        if (root is null || root.Name != "sprite")
        {
            throw new InvalidDataException($"File '{fullPath}' is not a valid sprite GMX file.");
        }

        var sprite = new Sprite
        {
            Name = GetResourceNameFromPath(fullPath, ".sprite.gmx"),
        };

        foreach (var element in root.Elements())
        {
            switch (element.Name.LocalName)
            {
                case "type":
                    sprite.Type = (SpriteType)ReadInt32(element);
                    break;
                case "xorig":
                    sprite.XOrigin = ReadInt32(element);
                    break;
                case "yorigin":
                    sprite.YOrigin = ReadInt32(element);
                    break;
                case "colkind":
                    sprite.CollisionKind = (SpriteCollisionKind)ReadInt32(element);
                    break;
                case "coltolerance":
                    sprite.CollisionTolerance = ReadInt32(element);
                    break;
                case "sepmasks":
                    sprite.SeparateCollisionMasks = ReadGameMakerBoolean(element);
                    break;
                case "bboxmode":
                    sprite.BoundingBoxMode = (SpriteBoundingBoxMode)ReadInt32(element);
                    break;
                case "bbox_left":
                    sprite.BoundingBoxLeft = ReadInt32(element);
                    break;
                case "bbox_right":
                    sprite.BoundingBoxRight = ReadInt32(element);
                    break;
                case "bbox_top":
                    sprite.BoundingBoxTop = ReadInt32(element);
                    break;
                case "bbox_bottom":
                    sprite.BoundingBoxBottom = ReadInt32(element);
                    break;
                case "HTile":
                    sprite.HTile = ReadGameMakerBoolean(element);
                    break;
                case "VTile":
                    sprite.VTile = ReadGameMakerBoolean(element);
                    break;
                case "TextureGroup":
                    sprite.TextureGroups.Clear();
                    sprite.TextureGroups.Add(ReadInt32(element));
                    break;
                case "TextureGroups":
                    ReadTextureGroups(element, sprite.TextureGroups);
                    break;
                case "For3D":
                    sprite.For3D = ReadGameMakerBoolean(element);
                    break;
                case "DynamicTexturePage":
                    sprite.DynamicTexturePage = ReadGameMakerBoolean(element);
                    break;
                case "width":
                    sprite.Width = ReadInt32(element);
                    break;
                case "height":
                    sprite.Height = ReadInt32(element);
                    break;
                case "frames":
                    ReadFrames(element, sprite, fullPath);
                    break;
                case "SWFfile":
                    sprite.SwfFile = ReadRelativePath(element);
                    break;
                case "SWFprecision":
                    sprite.SwfPrecision = ReadSingle(element);
                    break;
                case "SpineFile":
                    sprite.SpineFile = ReadRelativePath(element);
                    break;
            }
        }

        RegisterSprite(sprite);
        return sprite;
    }

    private static void ReadTextureGroups(XElement textureGroupsElement, List<int> textureGroups)
    {
        textureGroups.Clear();

        var orderedGroupElements = textureGroupsElement
            .Elements()
            .OrderBy(static element => GetTextureGroupIndex(element.Name.LocalName));

        foreach (var groupElement in orderedGroupElements)
        {
            textureGroups.Add(ReadInt32(groupElement));
        }
    }

    private static void ReadIndexedGroups(XElement groupsElement, List<int> groups, string prefix)
    {
        groups.Clear();

        var orderedGroupElements = groupsElement
            .Elements()
            .OrderBy(element => GetIndexedElementSuffix(element.Name.LocalName, prefix));

        foreach (var groupElement in orderedGroupElements)
        {
            groups.Add(ReadInt32(groupElement));
        }
    }

    private static void ReadFrames(XElement framesElement, Sprite sprite, string spriteGmxPath)
    {
        var orderedFrames = framesElement
            .Elements("frame")
            .Select((frameElement, position) => new SpriteFrame
            {
                Index = ReadFrameIndex(frameElement, position),
                Bitmap = ReadBitmap(spriteGmxPath, ReadText(frameElement)),
            })
            .OrderBy(static frame => frame.Index);

        foreach (var frame in orderedFrames)
        {
            sprite.Frames.Add(frame);
        }
    }

    private static void ReadTimelineMoments(XElement timelineElement, List<TimelineMoment> moments)
    {
        moments.Clear();

        foreach (var entryElement in timelineElement.Elements("entry"))
        {
            var eventElement = entryElement.Element("event");
            if (eventElement is null || !eventElement.Elements("action").Any())
            {
                continue;
            }

            var moment = new TimelineMoment
            {
                Step = ReadInt32(entryElement.Element("step")
                    ?? throw new InvalidDataException("Timeline entry is missing required 'step' element.")),
            };

            ReadGameObjectActions(eventElement, moment.Actions);
            moments.Add(moment);
        }
    }

    private static void ReadExtensionFrameworks(XElement frameworksElement, List<ExtensionFramework> frameworks)
    {
        frameworks.Clear();

        foreach (var frameworkElement in frameworksElement.Elements())
        {
            frameworks.Add(new ExtensionFramework
            {
                Name = ReadText(frameworkElement),
                WeakReference = ReadOptionalAttributeString(frameworkElement, "weak") is "1" or "-1",
            });
        }
    }

    private static void ReadProjectConfigurations(XElement? configsElement, List<string> configurations)
    {
        configurations.Clear();

        if (configsElement is null)
        {
            return;
        }

        foreach (var configElement in configsElement.Elements("Config"))
        {
            configurations.Add(ReadText(configElement));
        }
    }

    private static void ReadProjectConfigurationFiles(
        string projectDirectory,
        IEnumerable<string> configurations,
        List<ProjectConfigurationFile> configurationFiles)
    {
        configurationFiles.Clear();

        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var configuration in configurations)
        {
            if (string.IsNullOrWhiteSpace(configuration))
            {
                continue;
            }

            var normalizedConfiguration = NormalizeProjectPath(configuration);
            AddProjectConfigurationFile(projectDirectory, normalizedConfiguration + ".config.gmx", configurationFiles, visited);

            var configurationDirectory = Path.Combine(projectDirectory, normalizedConfiguration);
            if (!Directory.Exists(configurationDirectory))
            {
                continue;
            }

            foreach (var filePath in Directory.EnumerateFiles(configurationDirectory, "*", SearchOption.AllDirectories)
                .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase))
            {
                var relativePath = Path.GetRelativePath(projectDirectory, filePath);
                AddProjectConfigurationFile(projectDirectory, relativePath, configurationFiles, visited);
            }
        }
    }

    private static void AddProjectConfigurationFile(
        string projectDirectory,
        string relativePath,
        List<ProjectConfigurationFile> configurationFiles,
        HashSet<string> visited)
    {
        var normalizedPath = NormalizeProjectPath(relativePath);
        if (!visited.Add(normalizedPath))
        {
            return;
        }

        var fullPath = Path.Combine(projectDirectory, normalizedPath);
        if (!File.Exists(fullPath))
        {
            return;
        }

        configurationFiles.Add(new ProjectConfigurationFile
        {
            FilePath = normalizedPath,
            RawData = File.ReadAllBytes(fullPath),
        });
    }

    private static void ReadProjectConstants(XElement? constantsElement, List<ProjectConstant> constants)
    {
        constants.Clear();

        if (constantsElement is null)
        {
            return;
        }

        foreach (var constantElement in constantsElement.Elements("constant"))
        {
            constants.Add(new ProjectConstant
            {
                Name = ReadOptionalAttributeString(constantElement, "name") ?? string.Empty,
                Value = ReadText(constantElement),
            });
        }
    }

    private static void ReadProjectHelp(XElement? helpElement, ProjectHelp help, string projectDirectory)
    {
        help.RtfFileName = string.Empty;
        help.Content = string.Empty;

        if (helpElement is null)
        {
            return;
        }

        var rtfElement = helpElement.Element("rtf");
        if (rtfElement is null)
        {
            return;
        }

        help.RtfFileName = ReadText(rtfElement);
        if (string.IsNullOrWhiteSpace(help.RtfFileName))
        {
            return;
        }

        var helpPath = ResolveProjectResourcePath(projectDirectory, help.RtfFileName);
        if (File.Exists(helpPath))
        {
            help.Content = File.ReadAllText(helpPath);
        }
    }

    private static void ReadProjectTutorialState(XElement? tutorialStateElement, ProjectTutorialState tutorialState)
    {
        tutorialState.IsTutorial = false;
        tutorialState.TutorialName = string.Empty;
        tutorialState.TutorialPage = 0;

        if (tutorialStateElement is null)
        {
            return;
        }

        foreach (var element in tutorialStateElement.Elements())
        {
            switch (element.Name.LocalName)
            {
                case "IsTutorial":
                    tutorialState.IsTutorial = ReadGameMakerBoolean(element);
                    break;
                case "TutorialName":
                    tutorialState.TutorialName = ReadText(element);
                    break;
                case "TutorialPage":
                    tutorialState.TutorialPage = ReadInt32(element);
                    break;
            }
        }
    }

    private static Dictionary<string, TResource> LoadProjectGroupedResources<TResource>(
        XElement? rootElement,
        string groupElementName,
        string itemElementName,
        Func<string, TResource> loader,
        List<TResource> target,
        Action<XElement, TResource>? configureResource = null)
        where TResource : Resource
    {
        target.Clear();

        var resourcesByPath = new Dictionary<string, TResource>(StringComparer.OrdinalIgnoreCase);
        if (rootElement is null)
        {
            return resourcesByPath;
        }

        foreach (var itemElement in EnumerateProjectResourceEntryElements(rootElement, groupElementName, itemElementName))
        {
            var entryPath = ReadText(itemElement);
            var resource = loader(entryPath);
            configureResource?.Invoke(itemElement, resource);
            target.Add(resource);
            resourcesByPath[NormalizeProjectPath(entryPath)] = resource;
        }

        return resourcesByPath;
    }

    private static Dictionary<string, TResource> LoadProjectFlatResources<TResource>(
        XElement? rootElement,
        string itemElementName,
        Func<string, TResource> loader,
        List<TResource> target)
        where TResource : Resource
    {
        target.Clear();

        var resourcesByPath = new Dictionary<string, TResource>(StringComparer.OrdinalIgnoreCase);
        if (rootElement is null)
        {
            return resourcesByPath;
        }

        foreach (var itemElement in rootElement.Elements(itemElementName))
        {
            var entryPath = ReadText(itemElement);
            var resource = loader(entryPath);
            target.Add(resource);
            resourcesByPath[NormalizeProjectPath(entryPath)] = resource;
        }

        return resourcesByPath;
    }

    private static IEnumerable<XElement> EnumerateProjectResourceEntryElements(XElement groupElement, string groupElementName, string itemElementName)
    {
        foreach (var childElement in groupElement.Elements())
        {
            if (childElement.Name.LocalName == groupElementName)
            {
                foreach (var entry in EnumerateProjectResourceEntryElements(childElement, groupElementName, itemElementName))
                {
                    yield return entry;
                }
            }
            else if (childElement.Name.LocalName == itemElementName)
            {
                yield return childElement;
            }
        }
    }

    private static void AddProjectResourceTreeNode(
        List<ProjectResourceTreeNode> target,
        XElement? element,
        Func<XElement, ProjectResourceTreeNode> builder)
    {
        if (element is null)
        {
            return;
        }

        target.Add(builder(element));
    }

    private static ProjectResourceTreeNode BuildProjectGroupedResourceTree<TResource>(
        XElement groupElement,
        string groupElementName,
        string itemElementName,
        ProjectResourceKind kind,
        Dictionary<string, TResource> resourcesByPath)
        where TResource : Resource
    {
        var node = new ProjectResourceTreeNode
        {
            Name = GetProjectTreeNodeName(groupElement),
            Kind = kind,
        };

        foreach (var childElement in groupElement.Elements())
        {
            if (childElement.Name.LocalName == groupElementName)
            {
                node.Children.Add(BuildProjectGroupedResourceTree(childElement, groupElementName, itemElementName, kind, resourcesByPath));
            }
            else if (childElement.Name.LocalName == itemElementName)
            {
                var resourcePath = NormalizeProjectPath(ReadText(childElement));
                if (!resourcesByPath.TryGetValue(resourcePath, out var resource))
                {
                    continue;
                }

                node.Children.Add(new ProjectResourceTreeNode
                {
                    Name = resource.Name,
                    Kind = kind,
                    Resource = resource,
                });
            }
        }

        return node;
    }

    private static ProjectResourceTreeNode BuildProjectFlatResourceTree<TResource>(
        XElement groupElement,
        ProjectResourceKind kind,
        string itemElementName,
        Dictionary<string, TResource> resourcesByPath,
        string defaultName)
        where TResource : Resource
    {
        var node = new ProjectResourceTreeNode
        {
            Name = GetProjectTreeNodeName(groupElement, defaultName),
            Kind = kind,
        };

        foreach (var itemElement in groupElement.Elements(itemElementName))
        {
            var resourcePath = NormalizeProjectPath(ReadText(itemElement));
            if (!resourcesByPath.TryGetValue(resourcePath, out var resource))
            {
                continue;
            }

            node.Children.Add(new ProjectResourceTreeNode
            {
                Name = resource.Name,
                Kind = kind,
                Resource = resource,
            });
        }

        return node;
    }

    private static ProjectResourceTreeNode BuildProjectDataFilesTree(
        XElement datafilesElement,
        Dictionary<string, DataFile> dataFilesByPath,
        string currentRelativeDirectory)
    {
        var node = new ProjectResourceTreeNode
        {
            Name = GetProjectTreeNodeName(datafilesElement, "datafiles"),
            Kind = ProjectResourceKind.DataFile,
        };

        foreach (var childElement in datafilesElement.Elements())
        {
            switch (childElement.Name.LocalName)
            {
                case "datafiles":
                {
                    var folderName = ReadOptionalAttributeString(childElement, "name");
                    var childRelativeDirectory = string.IsNullOrWhiteSpace(folderName)
                        ? currentRelativeDirectory
                        : NormalizeProjectPath(Path.Combine(currentRelativeDirectory, folderName));

                    node.Children.Add(BuildProjectDataFilesTree(childElement, dataFilesByPath, childRelativeDirectory));
                    break;
                }
                case "datafile":
                {
                    var fileNameElement = childElement.Element("name");
                    if (fileNameElement is null)
                    {
                        break;
                    }

                    var fileName = ReadText(fileNameElement);
                    var resourcePath = NormalizeProjectPath(Path.Combine(currentRelativeDirectory, fileName));
                    if (!dataFilesByPath.TryGetValue(resourcePath, out var dataFile))
                    {
                        break;
                    }

                    node.Children.Add(new ProjectResourceTreeNode
                    {
                        Name = dataFile.Name,
                        Kind = ProjectResourceKind.DataFile,
                        Resource = dataFile,
                    });

                    break;
                }
            }
        }

        return node;
    }

    private static void ReadExtensionIncludes(XElement filesElement, string extensionGmxPath, List<ExtensionInclude> includes)
    {
        includes.Clear();

        foreach (var fileElement in filesElement.Elements("file"))
        {
            var include = new ExtensionInclude();

            foreach (var element in fileElement.Elements())
            {
                switch (element.Name.LocalName)
                {
                    case "filename":
                        include.FileName = ReadText(element);
                        break;
                    case "origname":
                        include.OriginalName = ReadText(element);
                        break;
                    case "init":
                        include.Init = ReadText(element);
                        break;
                    case "final":
                        include.Final = ReadText(element);
                        break;
                    case "kind":
                        include.Kind = ReadInt32(element);
                        break;
                    case "uncompress":
                        include.Uncompress = ReadGameMakerBoolean(element);
                        break;
                    case "ConfigOptions":
                        ReadConfigOptions(element, include.ConfigOptions);
                        break;
                    case "functions":
                        ReadExtensionFunctions(element, include.Functions);
                        break;
                    case "constants":
                        ReadExtensionConstants(element, include.Constants);
                        break;
                    case "ProxyFiles":
                        ReadExtensionProxyFiles(element, extensionGmxPath, include.ProxyFiles);
                        break;
                }
            }

            include.RawData = ReadExtensionFileData(extensionGmxPath, include.FileName);
            includes.Add(include);
        }
    }

    private static void ReadExtensionIncludedResources(
        XElement includedResourcesElement,
        string extensionGmxPath,
        List<ExtensionIncludedResource> includedResources)
    {
        includedResources.Clear();

        foreach (var resourceElement in includedResourcesElement.Elements("Resource"))
        {
            var filePath = ReadText(resourceElement);
            if (string.IsNullOrWhiteSpace(filePath))
            {
                continue;
            }

            includedResources.Add(new ExtensionIncludedResource
            {
                FilePath = filePath,
                RawData = ReadExtensionIncludedResourceData(extensionGmxPath, filePath),
            });
        }
    }

    private static void ReadExtensionFunctions(XElement functionsElement, List<ExtensionFunction> functions)
    {
        functions.Clear();

        foreach (var functionElement in functionsElement.Elements("function"))
        {
            var function = new ExtensionFunction();

            foreach (var element in functionElement.Elements())
            {
                switch (element.Name.LocalName)
                {
                    case "name":
                        function.Name = ReadText(element);
                        break;
                    case "externalName":
                        function.ExternalName = ReadText(element);
                        break;
                    case "kind":
                        function.Kind = ReadInt32(element);
                        break;
                    case "help":
                        function.Help = ReadText(element);
                        break;
                    case "returnType":
                        function.ReturnType = ReadInt32(element);
                        break;
                    case "argCount":
                        function.ArgCount = ReadInt32(element);
                        break;
                    case "args":
                        ReadExtensionFunctionArguments(element, function.Args);
                        break;
                }
            }

            functions.Add(function);
        }
    }

    private static void ReadExtensionFunctionArguments(XElement argsElement, List<int> args)
    {
        args.Clear();

        foreach (var argElement in argsElement.Elements("arg"))
        {
            args.Add(ReadInt32(argElement));
        }
    }

    private static void ReadExtensionConstants(XElement constantsElement, List<ExtensionConstant> constants)
    {
        constants.Clear();

        foreach (var constantElement in constantsElement.Elements("constant"))
        {
            var constant = new ExtensionConstant();

            foreach (var element in constantElement.Elements())
            {
                switch (element.Name.LocalName)
                {
                    case "name":
                        constant.Name = ReadText(element);
                        break;
                    case "value":
                        constant.Value = ReadText(element);
                        break;
                }
            }

            constants.Add(constant);
        }
    }

    private static void ReadExtensionProxyFiles(XElement proxyFilesElement, string extensionGmxPath, List<ExtensionProxyFile> proxyFiles)
    {
        proxyFiles.Clear();

        foreach (var proxyFileElement in proxyFilesElement.Elements("ProxyFile"))
        {
            var proxyFile = new ExtensionProxyFile();

            foreach (var element in proxyFileElement.Elements())
            {
                switch (element.Name.LocalName)
                {
                    case "Name":
                        proxyFile.Name = ReadText(element);
                        break;
                    case "TargetMask":
                        proxyFile.TargetMask = long.Parse(element.Value, CultureInfo.InvariantCulture);
                        break;
                }
            }

            proxyFile.RawData = ReadExtensionFileData(extensionGmxPath, proxyFile.Name);
            proxyFiles.Add(proxyFile);
        }
    }

    private static void ReadDataFilesTree(
        XElement datafilesElement,
        string currentRelativeDirectory,
        string projectDirectory,
        List<DataFile> dataFiles)
    {
        foreach (var childElement in datafilesElement.Elements())
        {
            switch (childElement.Name.LocalName)
            {
                case "datafiles":
                {
                    var folderName = ReadOptionalAttributeString(childElement, "name");
                    var childRelativeDirectory = string.IsNullOrWhiteSpace(folderName)
                        ? currentRelativeDirectory
                        : Path.Combine(currentRelativeDirectory, folderName);

                    ReadDataFilesTree(childElement, childRelativeDirectory, projectDirectory, dataFiles);
                    break;
                }
                case "datafile":
                    dataFiles.Add(ReadDataFile(childElement, currentRelativeDirectory, projectDirectory));
                    break;
            }
        }
    }

    private static DataFile ReadDataFile(XElement dataFileElement, string relativeDirectory, string projectDirectory)
    {
        var dataFile = new DataFile();

        foreach (var element in dataFileElement.Elements())
        {
            switch (element.Name.LocalName)
            {
                case "name":
                    dataFile.Name = ReadText(element);
                    dataFile.OriginalName = dataFile.Name;
                    break;
                case "exists":
                    dataFile.Exists = ReadGameMakerBoolean(element);
                    break;
                case "size":
                    dataFile.Size = ReadInt32(element);
                    break;
                case "exportAction":
                    dataFile.ExportAction = ReadInt32(element);
                    break;
                case "exportDir":
                    dataFile.ExportDir = ReadText(element);
                    break;
                case "overwrite":
                    dataFile.Overwrite = ReadGameMakerBoolean(element);
                    break;
                case "freeData":
                    dataFile.FreeData = ReadGameMakerBoolean(element);
                    break;
                case "removeEnd":
                    dataFile.RemoveEnd = ReadGameMakerBoolean(element);
                    break;
                case "store":
                    dataFile.Store = ReadGameMakerBoolean(element);
                    break;
                case "ConfigOptions":
                    ReadConfigOptions(element, dataFile.ConfigOptions);
                    break;
            }
        }

        dataFile.FileName = string.IsNullOrWhiteSpace(dataFile.Name)
            ? relativeDirectory
            : Path.Combine(relativeDirectory, dataFile.Name);

        var fullFilePath = Path.Combine(projectDirectory, dataFile.FileName);
        if (dataFile.Exists && File.Exists(fullFilePath))
        {
            dataFile.RawData = File.ReadAllBytes(fullFilePath);

            if (dataFile.Size == 0)
            {
                dataFile.Size = dataFile.RawData.Length;
            }
        }

        return dataFile;
    }

    private static void ReadConfigOptions(XElement configOptionsElement, Dictionary<string, long> configOptions)
    {
        configOptions.Clear();

        foreach (var configElement in configOptionsElement.Elements("Config"))
        {
            var configName = ReadOptionalAttributeString(configElement, "name");
            if (string.IsNullOrWhiteSpace(configName))
            {
                continue;
            }

            var copyToMaskElement = configElement.Element("CopyToMask");
            if (copyToMaskElement is null)
            {
                continue;
            }

            configOptions[configName] = long.Parse(copyToMaskElement.Value, CultureInfo.InvariantCulture);
        }
    }

    private void ReadRoomMakerSettings(XElement makerSettingsElement, RoomMakerSettings makerSettings)
    {
        foreach (var element in makerSettingsElement.Elements())
        {
            switch (element.Name.LocalName)
            {
                case "isSet":
                    makerSettings.IsSet = ReadGameMakerBoolean(element);
                    break;
                case "w":
                    makerSettings.Width = ReadInt32(element);
                    break;
                case "h":
                    makerSettings.Height = ReadInt32(element);
                    break;
                case "showGrid":
                    makerSettings.ShowGrid = ReadGameMakerBoolean(element);
                    break;
                case "showObjects":
                    makerSettings.ShowObjects = ReadGameMakerBoolean(element);
                    break;
                case "showTiles":
                    makerSettings.ShowTiles = ReadGameMakerBoolean(element);
                    break;
                case "showBackgrounds":
                    makerSettings.ShowBackgrounds = ReadGameMakerBoolean(element);
                    break;
                case "showForegrounds":
                    makerSettings.ShowForegrounds = ReadGameMakerBoolean(element);
                    break;
                case "showViews":
                    makerSettings.ShowViews = ReadGameMakerBoolean(element);
                    break;
                case "deleteUnderlyingObj":
                    makerSettings.DeleteUnderlyingObj = ReadGameMakerBoolean(element);
                    break;
                case "deleteUnderlyingTiles":
                    makerSettings.DeleteUnderlyingTiles = ReadGameMakerBoolean(element);
                    break;
                case "page":
                    makerSettings.Page = ReadInt32(element);
                    break;
                case "xoffset":
                    makerSettings.XOffset = ReadInt32(element);
                    break;
                case "yoffset":
                    makerSettings.YOffset = ReadInt32(element);
                    break;
            }
        }
    }

    private void ReadRoomBackgrounds(XElement backgroundsElement, List<RoomBackground> backgrounds)
    {
        backgrounds.Clear();

        foreach (var backgroundElement in backgroundsElement.Elements("background"))
        {
            var roomBackground = new RoomBackground
            {
                Visible = ReadRequiredAttributeBoolean(backgroundElement, "visible"),
                Foreground = ReadRequiredAttributeBoolean(backgroundElement, "foreground"),
                X = ReadRequiredAttributeInt32(backgroundElement, "x"),
                Y = ReadRequiredAttributeInt32(backgroundElement, "y"),
                HTiled = ReadRequiredAttributeBoolean(backgroundElement, "htiled"),
                VTiled = ReadRequiredAttributeBoolean(backgroundElement, "vtiled"),
                HSpeed = ReadRequiredAttributeInt32(backgroundElement, "hspeed"),
                VSpeed = ReadRequiredAttributeInt32(backgroundElement, "vspeed"),
                Stretch = ReadRequiredAttributeBoolean(backgroundElement, "stretch"),
            };

            AssignRoomBackgroundReference(roomBackground, ReadOptionalAttributeString(backgroundElement, "name"));
            backgrounds.Add(roomBackground);
        }
    }

    private void ReadRoomViews(XElement viewsElement, List<RoomView> views)
    {
        views.Clear();

        foreach (var viewElement in viewsElement.Elements("view"))
        {
            var view = new RoomView
            {
                Visible = ReadRequiredAttributeBoolean(viewElement, "visible"),
                XView = ReadRequiredAttributeInt32(viewElement, "xview"),
                YView = ReadRequiredAttributeInt32(viewElement, "yview"),
                WView = ReadRequiredAttributeInt32(viewElement, "wview"),
                HView = ReadRequiredAttributeInt32(viewElement, "hview"),
                XPort = ReadRequiredAttributeInt32(viewElement, "xport"),
                YPort = ReadRequiredAttributeInt32(viewElement, "yport"),
                WPort = ReadRequiredAttributeInt32(viewElement, "wport"),
                HPort = ReadRequiredAttributeInt32(viewElement, "hport"),
                HBorder = ReadRequiredAttributeInt32(viewElement, "hborder"),
                VBorder = ReadRequiredAttributeInt32(viewElement, "vborder"),
                HSpeed = ReadRequiredAttributeInt32(viewElement, "hspeed"),
                VSpeed = ReadRequiredAttributeInt32(viewElement, "vspeed"),
            };

            AssignRoomViewFollowObjectReference(view, ReadOptionalAttributeString(viewElement, "objName"));
            views.Add(view);
        }
    }

    private void ReadRoomInstances(XElement instancesElement, List<RoomInstance> instances)
    {
        instances.Clear();

        foreach (var instanceElement in instancesElement.Elements("instance"))
        {
            var roomInstance = new RoomInstance
            {
                Name = ReadOptionalAttributeString(instanceElement, "name") ?? string.Empty,
                Id = ReadOptionalAttributeInt32(instanceElement, "id", defaultValue: -1),
                X = ReadRequiredAttributeInt32(instanceElement, "x"),
                Y = ReadRequiredAttributeInt32(instanceElement, "y"),
                Code = ReadOptionalAttributeString(instanceElement, "code") ?? string.Empty,
                ScaleX = ReadOptionalAttributeDouble(instanceElement, "scaleX", 1.0),
                ScaleY = ReadOptionalAttributeDouble(instanceElement, "scaleY", 1.0),
                Colour = ReadOptionalAttributeUInt32(instanceElement, "colour", uint.MaxValue),
                Rotation = ReadOptionalAttributeDouble(instanceElement, "rotation", 0.0),
            };

            if (roomInstance.Id < 0)
            {
                roomInstance.Id = _nextRoomInstanceId++;
            }

            AssignRoomInstanceObjectReference(roomInstance, ReadOptionalAttributeString(instanceElement, "objName"));
            instances.Add(roomInstance);
        }
    }

    private void ReadRoomTiles(XElement tilesElement, List<RoomTile> tiles)
    {
        tiles.Clear();

        foreach (var tileElement in tilesElement.Elements("tile"))
        {
            var colour = ReadOptionalAttributeUInt32(tileElement, "colour", uint.MaxValue);
            var roomTile = new RoomTile
            {
                Id = ReadOptionalAttributeInt32(tileElement, "id", defaultValue: -1),
                X = ReadRequiredAttributeInt32(tileElement, "x"),
                Y = ReadRequiredAttributeInt32(tileElement, "y"),
                Width = ReadRequiredAttributeInt32(tileElement, "w"),
                Height = ReadRequiredAttributeInt32(tileElement, "h"),
                SourceX = ReadRequiredAttributeInt32(tileElement, "xo"),
                SourceY = ReadRequiredAttributeInt32(tileElement, "yo"),
                Depth = ReadRequiredAttributeInt32(tileElement, "depth"),
                ScaleX = ReadOptionalAttributeDouble(tileElement, "scaleX", 1.0),
                ScaleY = ReadOptionalAttributeDouble(tileElement, "scaleY", 1.0),
                Blend = ConvertGameMakerColourToRgb(colour),
                Alpha = (double)(colour >> 24) / byte.MaxValue,
            };

            if (roomTile.Id < 0)
            {
                roomTile.Id = _nextRoomTileId++;
            }

            AssignRoomTileBackgroundReference(roomTile, ReadOptionalAttributeString(tileElement, "bgName"));
            tiles.Add(roomTile);
        }
    }

    private void ReadGameObjectEvents(XElement eventsElement, List<GameObjectEvent> events)
    {
        events.Clear();

        foreach (var eventElement in eventsElement.Elements("event"))
        {
            var eventType = (GameObjectEventType)ReadRequiredAttributeInt32(eventElement, "eventtype");
            var gameObjectEvent = new GameObjectEvent
            {
                EventType = eventType,
                EventNumber = ReadOptionalAttributeInt32(eventElement, "enumb"),
            };

            if (eventType == GameObjectEventType.Collision)
            {
                AssignCollisionObjectReference(gameObjectEvent, ReadOptionalAttributeString(eventElement, "ename"));
            }

            ReadGameObjectActions(eventElement, gameObjectEvent.Actions);
            events.Add(gameObjectEvent);
        }
    }

    private static void ReadGameObjectActions(XElement eventElement, List<GameObjectAction> actions)
    {
        actions.Clear();

        foreach (var actionElement in eventElement.Elements("action"))
        {
            var action = new GameObjectAction();

            foreach (var element in actionElement.Elements())
            {
                switch (element.Name.LocalName)
                {
                    case "libid":
                        action.LibId = ReadInt32(element);
                        break;
                    case "id":
                        action.Id = ReadInt32(element);
                        break;
                    case "kind":
                        action.Kind = (GameObjectActionKind)ReadInt32(element);
                        break;
                    case "userelative":
                        action.UseRelative = ReadGameMakerBoolean(element);
                        break;
                    case "isquestion":
                        action.IsQuestion = ReadGameMakerBoolean(element);
                        break;
                    case "useapplyto":
                        action.UseApplyTo = ReadGameMakerBoolean(element);
                        break;
                    case "exetype":
                        action.ExecuteType = (GameObjectActionExecuteType)ReadInt32(element);
                        break;
                    case "functionname":
                        action.FunctionName = ReadText(element);
                        break;
                    case "codestring":
                        action.CodeString = ReadText(element);
                        break;
                    case "whoName":
                        action.WhoName = ReadText(element);
                        break;
                    case "relative":
                        action.Relative = ReadGameMakerBoolean(element);
                        break;
                    case "isnot":
                        action.IsNot = ReadGameMakerBoolean(element);
                        break;
                    case "arguments":
                        ReadGameObjectActionArguments(element, action.Arguments);
                        break;
                }
            }

            actions.Add(action);
        }
    }

    private static void ReadGameObjectActionArguments(XElement argumentsElement, List<GameObjectActionArgument> arguments)
    {
        arguments.Clear();

        foreach (var argumentElement in argumentsElement.Elements("argument"))
        {
            var argument = new GameObjectActionArgument();

            foreach (var element in argumentElement.Elements())
            {
                if (element.Name.LocalName == "kind")
                {
                    argument.Kind = (GameObjectActionArgumentKind)ReadInt32(element);
                }
                else
                {
                    argument.Value = ReadText(element);
                }
            }

            arguments.Add(argument);
        }
    }

    private static void ReadFontRanges(XElement rangesElement, List<FontRange> ranges)
    {
        ranges.Clear();

        foreach (var rangeElement in rangesElement.Elements())
        {
            var parts = ReadText(rangeElement)
                .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length != 2)
            {
                throw new InvalidDataException($"Font range '{rangeElement.Value}' is not in 'start,end' format.");
            }

            ranges.Add(new FontRange
            {
                Start = int.Parse(parts[0], CultureInfo.InvariantCulture),
                End = int.Parse(parts[1], CultureInfo.InvariantCulture),
            });
        }
    }

    private static void ReadFontGlyphs(XElement glyphsElement, List<FontGlyph> glyphs)
    {
        glyphs.Clear();

        foreach (var glyphElement in glyphsElement.Elements("glyph"))
        {
            glyphs.Add(new FontGlyph
            {
                Character = ReadRequiredAttributeInt32(glyphElement, "character"),
                X = ReadRequiredAttributeInt32(glyphElement, "x"),
                Y = ReadRequiredAttributeInt32(glyphElement, "y"),
                Width = ReadRequiredAttributeInt32(glyphElement, "w"),
                Height = ReadRequiredAttributeInt32(glyphElement, "h"),
                Shift = ReadRequiredAttributeInt32(glyphElement, "shift"),
                Offset = ReadRequiredAttributeInt32(glyphElement, "offset"),
            });
        }
    }

    private static void ReadFontKerningPairs(XElement kerningPairsElement, List<FontGlyph> glyphs)
    {
        foreach (var glyph in glyphs)
        {
            glyph.Kerning.Clear();
        }

        foreach (var pairElement in kerningPairsElement.Elements("pair"))
        {
            var first = ReadRequiredAttributeInt32(pairElement, "first");
            var second = ReadRequiredAttributeInt32(pairElement, "second");
            var amount = ReadRequiredAttributeInt32(pairElement, "amount");

            var glyph = glyphs.FirstOrDefault(existingGlyph => existingGlyph.Character == first);
            if (glyph is null)
            {
                continue;
            }

            glyph.Kerning.Add(new FontKerning
            {
                Other = second,
                Amount = amount,
            });
        }
    }

    private static void ReadPathPoints(XElement pointsElement, List<GamePathPoint> points)
    {
        points.Clear();

        foreach (var pointElement in pointsElement.Elements("point"))
        {
            var parts = ReadText(pointElement)
                .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length != 3)
            {
                throw new InvalidDataException($"Path point '{pointElement.Value}' is not in 'x,y,speed' format.");
            }

            points.Add(new GamePathPoint
            {
                X = double.Parse(parts[0], CultureInfo.InvariantCulture),
                Y = double.Parse(parts[1], CultureInfo.InvariantCulture),
                Speed = double.Parse(parts[2], CultureInfo.InvariantCulture),
            });
        }
    }

    private static void ReadPhysicsShapePoints(XElement physicsShapePointsElement, List<GameObjectPhysicsShapePoint> points)
    {
        points.Clear();

        foreach (var pointElement in physicsShapePointsElement.Elements("point"))
        {
            var parts = ReadText(pointElement)
                .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length != 2)
            {
                throw new InvalidDataException($"Physics shape point '{pointElement.Value}' is not in 'x,y' format.");
            }

            points.Add(new GameObjectPhysicsShapePoint
            {
                X = float.Parse(parts[0], CultureInfo.InvariantCulture),
                Y = float.Parse(parts[1], CultureInfo.InvariantCulture),
            });
        }
    }

    private static int ReadConfiguredInt32(XElement element)
    {
        return int.Parse(ReadConfiguredText(element), CultureInfo.InvariantCulture);
    }

    private static double ReadConfiguredDouble(XElement element)
    {
        return double.Parse(ReadConfiguredText(element), CultureInfo.InvariantCulture);
    }

    private static int ReadFrameIndex(XElement frameElement, int fallbackIndex)
    {
        var indexAttribute = frameElement.Attribute("index");
        return indexAttribute is null
            ? fallbackIndex
            : int.Parse(indexAttribute.Value, CultureInfo.InvariantCulture);
    }

    private static int ReadInt32(XElement element)
    {
        return int.Parse(element.Value, CultureInfo.InvariantCulture);
    }

    private static int ReadRequiredAttributeInt32(XElement element, string attributeName)
    {
        var attribute = element.Attribute(attributeName)
            ?? throw new InvalidDataException($"Element '{element.Name.LocalName}' is missing required attribute '{attributeName}'.");

        return int.Parse(attribute.Value, CultureInfo.InvariantCulture);
    }

    private static bool ReadRequiredAttributeBoolean(XElement element, string attributeName)
    {
        var attribute = element.Attribute(attributeName)
            ?? throw new InvalidDataException($"Element '{element.Name.LocalName}' is missing required attribute '{attributeName}'.");

        return ReadGameMakerBoolean(attribute.Value);
    }

    private static int ReadOptionalAttributeInt32(XElement element, string attributeName, int defaultValue = 0)
    {
        var attribute = element.Attribute(attributeName);
        return attribute is null
            ? defaultValue
            : int.Parse(attribute.Value, CultureInfo.InvariantCulture);
    }

    private static string? ReadOptionalAttributeString(XElement element, string attributeName)
    {
        return element.Attribute(attributeName)?.Value;
    }

    private static double ReadOptionalAttributeDouble(XElement element, string attributeName, double defaultValue)
    {
        var attribute = element.Attribute(attributeName);
        return attribute is null
            ? defaultValue
            : double.Parse(attribute.Value, CultureInfo.InvariantCulture);
    }

    private static uint ReadOptionalAttributeUInt32(XElement element, string attributeName, uint defaultValue)
    {
        var attribute = element.Attribute(attributeName);
        return attribute is null
            ? defaultValue
            : uint.Parse(attribute.Value, CultureInfo.InvariantCulture);
    }

    private static float ReadSingle(XElement element)
    {
        return float.Parse(element.Value, CultureInfo.InvariantCulture);
    }

    private static double ReadDouble(XElement element)
    {
        return double.Parse(element.Value, CultureInfo.InvariantCulture);
    }

    private static bool ReadGameMakerBoolean(XElement element)
    {
        var value = element.Value.Trim();

        return ReadGameMakerBoolean(value);
    }

    private static bool ReadGameMakerBoolean(string value)
    {
        value = value.Trim();

        return value switch
        {
            "-1" => true,
            "1" => true,
            "0" => false,
            _ => bool.Parse(value),
        };
    }

    private static string ReadRelativePath(XElement element)
    {
        return element.Value.Trim();
    }

    private static string ReadText(XElement element)
    {
        return element.Value.Trim();
    }

    private static string ReadConfiguredText(XElement element)
    {
        var firstChildElement = element.Elements().FirstOrDefault();
        return firstChildElement is null
            ? ReadText(element)
            : ReadText(firstChildElement);
    }

    private static Avalonia.Media.Imaging.Bitmap? ReadBitmap(string resourceGmxPath, string relativePath)
    {
        var resourceDirectory = Path.GetDirectoryName(resourceGmxPath);
        if (string.IsNullOrWhiteSpace(resourceDirectory))
        {
            return null;
        }

        var bitmapPath = Path.GetFullPath(Path.Combine(resourceDirectory, relativePath));
        return File.Exists(bitmapPath)
            ? new Avalonia.Media.Imaging.Bitmap(bitmapPath)
            : null;
    }

    private static Avalonia.Media.Imaging.Bitmap? ReadFontBitmap(string fontGmxPath)
    {
        var fontDirectory = Path.GetDirectoryName(fontGmxPath);
        if (string.IsNullOrWhiteSpace(fontDirectory))
        {
            return null;
        }

        var bitmapFileName = GetResourceNameFromPath(fontGmxPath, ".font.gmx") + ".png";
        var bitmapPath = Path.Combine(fontDirectory, bitmapFileName);

        return File.Exists(bitmapPath)
            ? new Avalonia.Media.Imaging.Bitmap(bitmapPath)
            : null;
    }

    private static byte[]? ReadExtensionFileData(string extensionGmxPath, string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return null;
        }

        var filePath = ResolveExtensionSidecarPath(extensionGmxPath, fileName);
        return filePath is null || !File.Exists(filePath)
            ? null
            : File.ReadAllBytes(filePath);
    }

    private static byte[]? ReadExtensionIncludedResourceData(string extensionGmxPath, string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return null;
        }

        var extensionFilePath = Path.GetFullPath(extensionGmxPath);
        var projectDirectory = Path.GetDirectoryName(Path.GetDirectoryName(extensionFilePath) ?? string.Empty);
        if (string.IsNullOrWhiteSpace(projectDirectory))
        {
            return null;
        }

        var fullPath = Path.Combine(projectDirectory, NormalizeProjectPath(filePath));
        return File.Exists(fullPath)
            ? File.ReadAllBytes(fullPath)
            : null;
    }

    private static string? ResolveExtensionSidecarPath(string extensionGmxPath, string fileName)
    {
        var extensionDirectory = Path.GetDirectoryName(extensionGmxPath);
        if (string.IsNullOrWhiteSpace(extensionDirectory))
        {
            return null;
        }

        var normalizedFileName = NormalizeProjectPath(fileName);
        var extensionName = GetResourceNameFromPath(extensionGmxPath, ".extension.gmx");
        var candidatePaths = new[]
        {
            Path.Combine(extensionDirectory, extensionName, normalizedFileName),
            Path.Combine(extensionDirectory, normalizedFileName),
        };

        return candidatePaths.FirstOrDefault(File.Exists)
            ?? candidatePaths[0];
    }

    private static string GetResourceNameFromPath(string fullPath, string expectedExtension)
    {
        var fileName = Path.GetFileName(fullPath);
        return fileName.EndsWith(expectedExtension, StringComparison.OrdinalIgnoreCase)
            ? fileName[..^expectedExtension.Length]
            : Path.GetFileNameWithoutExtension(fullPath);
    }

    private static int GetTextureGroupIndex(string elementName)
    {
        const string prefix = "TextureGroup";

        if (elementName.StartsWith(prefix, StringComparison.Ordinal)
            && int.TryParse(elementName[prefix.Length..], CultureInfo.InvariantCulture, out var index))
        {
            return index;
        }

        return int.MaxValue;
    }

    private static int GetIndexedElementSuffix(string elementName, string prefix)
    {
        if (elementName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            && int.TryParse(elementName[prefix.Length..], CultureInfo.InvariantCulture, out var index))
        {
            return index;
        }

        return int.MaxValue;
    }

    private static int MapCompressionQuality(int bitRate)
    {
        return bitRate switch
        {
            8 => 0,
            16 => 0,
            24 => 0,
            32 => 0,
            40 => 0,
            48 => 0,
            56 => 1,
            64 => 1,
            80 => 1,
            96 => 2,
            112 => 3,
            128 => 4,
            144 => 5,
            160 => 5,
            192 => 6,
            224 => 7,
            256 => 8,
            320 => 9,
            512 => 10,
            _ => 4,
        };
    }

    private void AssignSpriteReference(GameObject gameObject, string spriteName)
    {
        if (IsUndefinedReferenceName(spriteName))
        {
            gameObject.Sprite = null;
            return;
        }

        if (_spritesByName.TryGetValue(spriteName, out var sprite))
        {
            gameObject.Sprite = sprite;
        }
        else
        {
            _pendingSpriteReferences.Add((gameObject, spriteName));
        }
    }

    private void AssignMaskReference(GameObject gameObject, string maskName)
    {
        if (IsUndefinedReferenceName(maskName))
        {
            gameObject.Mask = null;
            return;
        }

        if (_spritesByName.TryGetValue(maskName, out var mask))
        {
            gameObject.Mask = mask;
        }
        else
        {
            _pendingMaskReferences.Add((gameObject, maskName));
        }
    }

    private void AssignParentReference(GameObject gameObject, string parentName)
    {
        if (IsUndefinedReferenceName(parentName))
        {
            gameObject.Parent = null;
            return;
        }

        if (_objectsByName.TryGetValue(parentName, out var parent))
        {
            gameObject.Parent = parent;
        }
        else
        {
            _pendingParentReferences.Add((gameObject, parentName));
        }
    }

    private void AssignCollisionObjectReference(GameObjectEvent gameObjectEvent, string? collisionObjectName)
    {
        if (string.IsNullOrWhiteSpace(collisionObjectName) || IsUndefinedReferenceName(collisionObjectName))
        {
            gameObjectEvent.CollisionObject = null;
            return;
        }

        if (_objectsByName.TryGetValue(collisionObjectName, out var collisionObject))
        {
            gameObjectEvent.CollisionObject = collisionObject;
        }
        else
        {
            _pendingCollisionObjectReferences.Add((gameObjectEvent, collisionObjectName));
        }
    }

    private void AssignRoomViewFollowObjectReference(RoomView roomView, string? objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName) || IsUndefinedReferenceName(objectName))
        {
            roomView.FollowObject = null;
            return;
        }

        if (_objectsByName.TryGetValue(objectName, out var gameObject))
        {
            roomView.FollowObject = gameObject;
        }
        else
        {
            _pendingRoomViewObjectReferences.Add((roomView, objectName));
        }
    }

    private void AssignRoomInstanceObjectReference(RoomInstance roomInstance, string? objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName) || IsUndefinedReferenceName(objectName))
        {
            roomInstance.Object = null;
            return;
        }

        if (_objectsByName.TryGetValue(objectName, out var gameObject))
        {
            roomInstance.Object = gameObject;
        }
        else
        {
            _pendingRoomInstanceObjectReferences.Add((roomInstance, objectName));
        }
    }

    private void AssignRoomBackgroundReference(RoomBackground roomBackground, string? backgroundName)
    {
        if (string.IsNullOrWhiteSpace(backgroundName) || IsUndefinedReferenceName(backgroundName))
        {
            roomBackground.Background = null;
            return;
        }

        if (_backgroundsByName.TryGetValue(backgroundName, out var background))
        {
            roomBackground.Background = background;
        }
        else
        {
            _pendingRoomBackgroundReferences.Add((roomBackground, backgroundName));
        }
    }

    private void AssignRoomTileBackgroundReference(RoomTile roomTile, string? backgroundName)
    {
        if (string.IsNullOrWhiteSpace(backgroundName) || IsUndefinedReferenceName(backgroundName))
        {
            roomTile.Background = null;
            return;
        }

        if (_backgroundsByName.TryGetValue(backgroundName, out var background))
        {
            roomTile.Background = background;
        }
        else
        {
            _pendingRoomTileBackgroundReferences.Add((roomTile, backgroundName));
        }
    }

    private void RegisterSprite(Sprite sprite)
    {
        if (string.IsNullOrWhiteSpace(sprite.Name))
        {
            return;
        }

        _spritesByName[sprite.Name] = sprite;
        ResolvePendingSpriteReferences(sprite);
        ResolvePendingMaskReferences(sprite);
    }

    private void RegisterBackground(Background background)
    {
        if (string.IsNullOrWhiteSpace(background.Name))
        {
            return;
        }

        _backgroundsByName[background.Name] = background;
        ResolvePendingRoomBackgroundReferences(background);
        ResolvePendingRoomTileBackgroundReferences(background);
    }

    private void RegisterGameObject(GameObject gameObject)
    {
        if (string.IsNullOrWhiteSpace(gameObject.Name))
        {
            return;
        }

        _objectsByName[gameObject.Name] = gameObject;
        ResolvePendingParentReferences(gameObject);
        ResolvePendingCollisionObjectReferences(gameObject);
        ResolvePendingRoomViewObjectReferences(gameObject);
        ResolvePendingRoomInstanceObjectReferences(gameObject);
    }

    private void ResolvePendingSpriteReferences(Sprite sprite)
    {
        for (var index = _pendingSpriteReferences.Count - 1; index >= 0; index--)
        {
            var pendingReference = _pendingSpriteReferences[index];
            if (!string.Equals(pendingReference.SpriteName, sprite.Name, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            pendingReference.Object.Sprite = sprite;
            _pendingSpriteReferences.RemoveAt(index);
        }
    }

    private void ResolvePendingMaskReferences(Sprite sprite)
    {
        for (var index = _pendingMaskReferences.Count - 1; index >= 0; index--)
        {
            var pendingReference = _pendingMaskReferences[index];
            if (!string.Equals(pendingReference.MaskName, sprite.Name, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            pendingReference.Object.Mask = sprite;
            _pendingMaskReferences.RemoveAt(index);
        }
    }

    private void ResolvePendingParentReferences(GameObject gameObject)
    {
        for (var index = _pendingParentReferences.Count - 1; index >= 0; index--)
        {
            var pendingReference = _pendingParentReferences[index];
            if (!string.Equals(pendingReference.ParentName, gameObject.Name, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            pendingReference.Object.Parent = gameObject;
            _pendingParentReferences.RemoveAt(index);
        }
    }

    private void ResolvePendingCollisionObjectReferences(GameObject gameObject)
    {
        for (var index = _pendingCollisionObjectReferences.Count - 1; index >= 0; index--)
        {
            var pendingReference = _pendingCollisionObjectReferences[index];
            if (!string.Equals(pendingReference.CollisionObjectName, gameObject.Name, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            pendingReference.Event.CollisionObject = gameObject;
            _pendingCollisionObjectReferences.RemoveAt(index);
        }
    }

    private void ResolvePendingRoomViewObjectReferences(GameObject gameObject)
    {
        for (var index = _pendingRoomViewObjectReferences.Count - 1; index >= 0; index--)
        {
            var pendingReference = _pendingRoomViewObjectReferences[index];
            if (!string.Equals(pendingReference.ObjectName, gameObject.Name, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            pendingReference.View.FollowObject = gameObject;
            _pendingRoomViewObjectReferences.RemoveAt(index);
        }
    }

    private void ResolvePendingRoomInstanceObjectReferences(GameObject gameObject)
    {
        for (var index = _pendingRoomInstanceObjectReferences.Count - 1; index >= 0; index--)
        {
            var pendingReference = _pendingRoomInstanceObjectReferences[index];
            if (!string.Equals(pendingReference.ObjectName, gameObject.Name, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            pendingReference.Instance.Object = gameObject;
            _pendingRoomInstanceObjectReferences.RemoveAt(index);
        }
    }

    private void ResolvePendingRoomBackgroundReferences(Background background)
    {
        for (var index = _pendingRoomBackgroundReferences.Count - 1; index >= 0; index--)
        {
            var pendingReference = _pendingRoomBackgroundReferences[index];
            if (!string.Equals(pendingReference.BackgroundName, background.Name, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            pendingReference.Background.Background = background;
            _pendingRoomBackgroundReferences.RemoveAt(index);
        }
    }

    private void ResolvePendingRoomTileBackgroundReferences(Background background)
    {
        for (var index = _pendingRoomTileBackgroundReferences.Count - 1; index >= 0; index--)
        {
            var pendingReference = _pendingRoomTileBackgroundReferences[index];
            if (!string.Equals(pendingReference.BackgroundName, background.Name, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            pendingReference.Tile.Background = background;
            _pendingRoomTileBackgroundReferences.RemoveAt(index);
        }
    }

    private static bool IsUndefinedReferenceName(string name)
    {
        return string.IsNullOrWhiteSpace(name)
            || string.Equals(name, "<undefined>", StringComparison.OrdinalIgnoreCase);
    }

    private void ResetParsingState()
    {
        _spritesByName.Clear();
        _backgroundsByName.Clear();
        _objectsByName.Clear();
        _pendingSpriteReferences.Clear();
        _pendingMaskReferences.Clear();
        _pendingParentReferences.Clear();
        _pendingCollisionObjectReferences.Clear();
        _pendingRoomViewObjectReferences.Clear();
        _pendingRoomInstanceObjectReferences.Clear();
        _pendingRoomBackgroundReferences.Clear();
        _pendingRoomTileBackgroundReferences.Clear();
        _nextRoomInstanceId = 100000;
        _nextRoomTileId = 10000000;
    }

    private static string ResolveProjectResourcePath(string projectDirectory, string relativePath, string? extensionSuffix = null)
    {
        var normalizedPath = NormalizeProjectPath(relativePath);
        if (!string.IsNullOrWhiteSpace(extensionSuffix)
            && !normalizedPath.EndsWith(extensionSuffix, StringComparison.OrdinalIgnoreCase))
        {
            normalizedPath += extensionSuffix;
        }

        return Path.GetFullPath(Path.Combine(projectDirectory, normalizedPath));
    }

    private static string NormalizeProjectPath(string path)
    {
        return path
            .Trim()
            .Replace('/', '\\');
    }

    private static string GetProjectTreeNodeName(XElement element, string? defaultName = null)
    {
        var name = ReadOptionalAttributeString(element, "name");
        if (!string.IsNullOrWhiteSpace(name))
        {
            return name;
        }

        return defaultName ?? element.Name.LocalName;
    }

    private static string? ResolveSoundFilePath(string soundGmxPath, string? originalName, string? dataFileName)
    {
        var soundDirectory = Path.GetDirectoryName(soundGmxPath);
        if (string.IsNullOrWhiteSpace(soundDirectory))
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(originalName))
        {
            var resolvedOriginalName = ResolveOriginalNamePath(soundDirectory, soundGmxPath, originalName);
            if (File.Exists(resolvedOriginalName))
            {
                return resolvedOriginalName;
            }
        }

        if (!string.IsNullOrWhiteSpace(dataFileName))
        {
            var dataPath = Path.Combine(soundDirectory, "audio", Path.GetFileName(dataFileName));
            if (File.Exists(dataPath))
            {
                return dataPath;
            }
        }

        return null;
    }

    private static string ResolveOriginalNamePath(string soundDirectory, string soundGmxPath, string originalName)
    {
        if (Path.IsPathRooted(originalName))
        {
            return originalName;
        }

        var candidatePath = originalName.StartsWith(soundDirectory, StringComparison.OrdinalIgnoreCase)
            ? originalName
            : Path.Combine(soundDirectory, "audio", Path.GetFileName(originalName));

        if (File.Exists(candidatePath))
        {
            return candidatePath;
        }

        var baseName = GetResourceNameFromPath(soundGmxPath, ".sound.gmx");
        var audioDirectory = Path.GetDirectoryName(candidatePath) ?? Path.Combine(soundDirectory, "audio");
        var originalExtension = Path.GetExtension(candidatePath);

        if (!string.IsNullOrWhiteSpace(originalExtension))
        {
            var sameExtensionPath = Path.Combine(audioDirectory, baseName + originalExtension);
            if (File.Exists(sameExtensionPath))
            {
                return sameExtensionPath;
            }
        }

        foreach (var extension in new[] { ".wav", ".mp3", ".ogg" })
        {
            var fallbackPath = Path.Combine(audioDirectory, baseName + extension);
            if (File.Exists(fallbackPath))
            {
                return fallbackPath;
            }
        }

        return candidatePath;
    }

    private static int ConvertGameMakerColourToRgb(uint colour)
    {
        return (int)(((colour & 0xFF0000) >> 16) | (colour & 0xFF00) | ((colour & 0xFF) << 16));
    }
}
