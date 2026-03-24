using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Avalonia.Media.Imaging;
using AvaloniaGM.Models;

namespace AvaloniaGM.Services;

public partial class ProjectGmxSerializer
{
    public void SerializeProject(string projectGmxPath, Project project)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectGmxPath);
        ArgumentNullException.ThrowIfNull(project);

        var fullPath = Path.GetFullPath(projectGmxPath);
        var projectDirectory = Path.GetDirectoryName(fullPath)
            ?? throw new InvalidDataException($"Unable to determine directory for project file '{fullPath}'.");

        Directory.CreateDirectory(projectDirectory);

        SerializeProjectConfigurationFiles(projectDirectory, project.ConfigurationFiles);
        SerializeHelp(projectDirectory, project.Help);
        SerializeExtensions(projectDirectory, project.Extensions);
        SerializeDataFiles(projectDirectory, project.DataFiles);
        SerializeSounds(projectDirectory, project.Sounds);
        SerializeSprites(projectDirectory, project.Sprites);
        SerializeBackgrounds(projectDirectory, project.Backgrounds);
        SerializePaths(projectDirectory, project.Paths);
        SerializeScripts(projectDirectory, project.Scripts);
        SerializeShaders(projectDirectory, project.Shaders);
        SerializeFonts(projectDirectory, project.Fonts);
        SerializeTimelines(projectDirectory, project.Timelines);
        SerializeGameObjects(projectDirectory, project.Objects);
        SerializeRooms(projectDirectory, project.Rooms);

        var root = new XElement("assets");

        root.Add(BuildConfigsElement(project.Configurations));
        root.Add(BuildDataFilesElement(project));
        root.Add(BuildExtensionsElement(project));
        root.Add(BuildGroupedProjectElement(project, ProjectResourceKind.Sound, "sounds", "sound", "sound"));
        root.Add(BuildGroupedProjectElement(project, ProjectResourceKind.Sprite, "sprites", "sprite", "sprites"));
        root.Add(BuildGroupedProjectElement(project, ProjectResourceKind.Background, "backgrounds", "background", "background"));
        root.Add(BuildGroupedProjectElement(project, ProjectResourceKind.Path, "paths", "path", "paths"));
        root.Add(BuildGroupedProjectElement(project, ProjectResourceKind.Script, "scripts", "script", "scripts"));
        root.Add(BuildGroupedProjectElement(project, ProjectResourceKind.Shader, "shaders", "shader", "shaders"));
        root.Add(BuildGroupedProjectElement(project, ProjectResourceKind.Font, "fonts", "font", "fonts"));
        root.Add(BuildGroupedProjectElement(project, ProjectResourceKind.Object, "objects", "object", "objects"));
        root.Add(BuildGroupedProjectElement(project, ProjectResourceKind.Timeline, "timelines", "timeline", "timelines"));
        root.Add(BuildGroupedProjectElement(project, ProjectResourceKind.Room, "rooms", "room", "rooms"));
        root.Add(BuildConstantsElement(project.Constants));
        root.Add(BuildHelpElement(project.Help));
        root.Add(BuildTutorialStateElement(project.TutorialState));

        SaveXml(fullPath, root);
    }

    private void SerializeSprites(string projectDirectory, IEnumerable<Sprite> sprites)
    {
        foreach (var sprite in sprites)
        {
            var fullPath = Path.Combine(projectDirectory, "sprites", sprite.Name + ".sprite.gmx");
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

            var framesElement = new XElement("frames");

            foreach (var frame in sprite.Frames.OrderBy(static frame => frame.Index))
            {
                var relativeImagePath = ToProjectPath(Path.Combine("images", $"{sprite.Name}_{frame.Index}.png"));
                framesElement.Add(new XElement(
                    "frame",
                    new XAttribute("index", frame.Index.ToString(CultureInfo.InvariantCulture)),
                    relativeImagePath));

                if (frame.Bitmap is not null)
                {
                    SaveBitmap(frame.Bitmap, Path.Combine(projectDirectory, "sprites", "images", $"{sprite.Name}_{frame.Index}.png"));
                }
            }

            var root = new XElement("sprite",
                new XElement("type", ((int)sprite.Type).ToString(CultureInfo.InvariantCulture)),
                new XElement("xorig", sprite.XOrigin.ToString(CultureInfo.InvariantCulture)),
                new XElement("yorigin", sprite.YOrigin.ToString(CultureInfo.InvariantCulture)),
                new XElement("colkind", ((int)sprite.CollisionKind).ToString(CultureInfo.InvariantCulture)),
                new XElement("coltolerance", sprite.CollisionTolerance.ToString(CultureInfo.InvariantCulture)),
                new XElement("sepmasks", ToGameMakerBoolean(sprite.SeparateCollisionMasks)),
                new XElement("bboxmode", ((int)sprite.BoundingBoxMode).ToString(CultureInfo.InvariantCulture)),
                new XElement("bbox_left", sprite.BoundingBoxLeft.ToString(CultureInfo.InvariantCulture)),
                new XElement("bbox_right", sprite.BoundingBoxRight.ToString(CultureInfo.InvariantCulture)),
                new XElement("bbox_top", sprite.BoundingBoxTop.ToString(CultureInfo.InvariantCulture)),
                new XElement("bbox_bottom", sprite.BoundingBoxBottom.ToString(CultureInfo.InvariantCulture)),
                new XElement("HTile", ToGameMakerBoolean(sprite.HTile)),
                new XElement("VTile", ToGameMakerBoolean(sprite.VTile)),
                BuildIndexedElements("TextureGroups", "TextureGroup", sprite.TextureGroups),
                new XElement("For3D", ToGameMakerBoolean(sprite.For3D)),
                new XElement("DynamicTexturePage", ToGameMakerBoolean(sprite.DynamicTexturePage)),
                new XElement("width", GetSpriteWidth(sprite).ToString(CultureInfo.InvariantCulture)),
                new XElement("height", GetSpriteHeight(sprite).ToString(CultureInfo.InvariantCulture)),
                framesElement);

            if (!string.IsNullOrWhiteSpace(sprite.SwfFile))
            {
                root.Add(new XElement("SWFfile", sprite.SwfFile));
                root.Add(new XElement("SWFprecision", ToInvariantString(sprite.SwfPrecision)));
            }

            if (!string.IsNullOrWhiteSpace(sprite.SpineFile))
            {
                root.Add(new XElement("SpineFile", sprite.SpineFile));
            }

            SaveXml(fullPath, root);
        }
    }

    private void SerializeBackgrounds(string projectDirectory, IEnumerable<Background> backgrounds)
    {
        foreach (var background in backgrounds)
        {
            var fullPath = Path.Combine(projectDirectory, "background", background.Name + ".background.gmx");
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

            var root = new XElement("background",
                new XElement("istileset", ToGameMakerBoolean(background.IsTileset)),
                new XElement("tilewidth", background.TileWidth.ToString(CultureInfo.InvariantCulture)),
                new XElement("tileheight", background.TileHeight.ToString(CultureInfo.InvariantCulture)),
                new XElement("tilexoff", background.TileXOffset.ToString(CultureInfo.InvariantCulture)),
                new XElement("tileyoff", background.TileYOffset.ToString(CultureInfo.InvariantCulture)),
                new XElement("tilehsep", background.TileHorizontalSeparation.ToString(CultureInfo.InvariantCulture)),
                new XElement("tilevsep", background.TileVerticalSeparation.ToString(CultureInfo.InvariantCulture)),
                new XElement("HTile", ToGameMakerBoolean(background.HTile)),
                new XElement("VTile", ToGameMakerBoolean(background.VTile)),
                BuildIndexedElements("TextureGroups", "TextureGroup", background.TextureGroups),
                new XElement("For3D", ToGameMakerBoolean(background.For3D)),
                new XElement("DynamicTexturePage", ToGameMakerBoolean(background.DynamicTexturePage)),
                new XElement("width", GetBackgroundWidth(background).ToString(CultureInfo.InvariantCulture)),
                new XElement("height", GetBackgroundHeight(background).ToString(CultureInfo.InvariantCulture)));

            if (background.Bitmap is not null)
            {
                var relativeImagePath = ToProjectPath(Path.Combine("images", background.Name + ".png"));
                root.Add(new XElement("data", relativeImagePath));
                SaveBitmap(background.Bitmap, Path.Combine(projectDirectory, "background", "images", background.Name + ".png"));
            }

            SaveXml(fullPath, root);
        }
    }

    private void SerializeSounds(string projectDirectory, IEnumerable<Sound> sounds)
    {
        foreach (var sound in sounds)
        {
            var fullPath = Path.Combine(projectDirectory, "sound", sound.Name + ".sound.gmx");
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

            var extension = ResolveSoundExtension(sound);
            var dataFileName = sound.Name + extension;
            var originalName = string.IsNullOrWhiteSpace(sound.OriginalName)
                ? ToProjectPath(Path.Combine("sound", "audio", dataFileName))
                : ToProjectPath(sound.OriginalName);

            if (sound.RawData is not null)
            {
                var audioPath = Path.Combine(projectDirectory, "sound", "audio", dataFileName);
                Directory.CreateDirectory(Path.GetDirectoryName(audioPath)!);
                File.WriteAllBytes(audioPath, sound.RawData);
            }

            var root = new XElement("sound",
                new XElement("kind", sound.Kind.ToString(CultureInfo.InvariantCulture)),
                new XElement("extension", extension),
                new XElement("origname", originalName),
                new XElement("effects", sound.Effects.ToString(CultureInfo.InvariantCulture)),
                new XElement("volume", new XElement("volume", ToInvariantString(sound.Volume))),
                new XElement("pan", ToInvariantString(sound.Pan)),
                new XElement("bitRates", new XElement("bitRate", MapCompressionQualityToBitRate(sound.CompressionQuality).ToString(CultureInfo.InvariantCulture))),
                new XElement("sampleRates", new XElement("sampleRate", sound.SampleRate.ToString(CultureInfo.InvariantCulture))),
                new XElement("types", new XElement("type", sound.Stereo ? "1" : "0")),
                new XElement("bitDepths", new XElement("bitDepth", sound.BitDepth.ToString(CultureInfo.InvariantCulture))),
                new XElement("preload", ToGameMakerBoolean(sound.Preload)),
                new XElement("data", dataFileName),
                new XElement("compressed", ToGameMakerBoolean(sound.Compressed)),
                new XElement("streamed", ToGameMakerBoolean(sound.Streamed)),
                new XElement("uncompressOnLoad", ToGameMakerBoolean(sound.UncompressOnLoad)),
                new XElement("audioGroup", sound.AudioGroup.ToString(CultureInfo.InvariantCulture)));

            if (!string.IsNullOrWhiteSpace(sound.ExportDirectory))
            {
                root.Add(new XElement("exportDir", sound.ExportDirectory));
            }

            SaveXml(fullPath, root);
        }
    }

    private void SerializePaths(string projectDirectory, IEnumerable<GamePath> paths)
    {
        foreach (var gamePath in paths)
        {
            var fullPath = Path.Combine(projectDirectory, "paths", gamePath.Name + ".path.gmx");
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

            var root = new XElement("path",
                new XElement("kind", gamePath.Kind.ToString(CultureInfo.InvariantCulture)),
                new XElement("closed", ToGameMakerBoolean(gamePath.Closed)),
                new XElement("precision", gamePath.Precision.ToString(CultureInfo.InvariantCulture)),
                new XElement("backroom", gamePath.BackRoom.ToString(CultureInfo.InvariantCulture)),
                new XElement("hsnap", gamePath.HSnap.ToString(CultureInfo.InvariantCulture)),
                new XElement("vsnap", gamePath.VSnap.ToString(CultureInfo.InvariantCulture)),
                new XElement("points",
                    gamePath.Points.Select(static point => new XElement(
                        "point",
                        string.Join(",",
                            ToInvariantString(point.X),
                            ToInvariantString(point.Y),
                            ToInvariantString(point.Speed))))));

            SaveXml(fullPath, root);
        }
    }

    private void SerializeFonts(string projectDirectory, IEnumerable<Font> fonts)
    {
        foreach (var font in fonts)
        {
            var fullPath = Path.Combine(projectDirectory, "fonts", font.Name + ".font.gmx");
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

            if (font.Bitmap is not null)
            {
                SaveBitmap(font.Bitmap, Path.Combine(projectDirectory, "fonts", font.Name + ".png"));
            }

            var glyphs = font.Glyphs.OrderBy(static glyph => glyph.Character).ToList();
            var root = new XElement("font",
                new XElement("name", font.FontName),
                new XElement("size", ToInvariantString(font.Size)),
                new XElement("bold", ToGameMakerBoolean(font.Bold)),
                new XElement("renderhq", ToGameMakerBoolean(font.AntiAlias > 0)),
                new XElement("italic", ToGameMakerBoolean(font.Italic)),
                new XElement("charset", font.CharSet.ToString(CultureInfo.InvariantCulture)),
                new XElement("aa", font.AntiAlias.ToString(CultureInfo.InvariantCulture)),
                new XElement("includeTTF", "0"),
                new XElement("TTFName", string.Empty),
                BuildIndexedElements("texgroups", "texgroup", font.TextureGroups),
                BuildFontRangesElement(font.Ranges, font.First, font.Last),
                new XElement("glyphs",
                    glyphs.Select(static glyph => new XElement("glyph",
                        new XAttribute("character", glyph.Character.ToString(CultureInfo.InvariantCulture)),
                        new XAttribute("x", glyph.X.ToString(CultureInfo.InvariantCulture)),
                        new XAttribute("y", glyph.Y.ToString(CultureInfo.InvariantCulture)),
                        new XAttribute("w", glyph.Width.ToString(CultureInfo.InvariantCulture)),
                        new XAttribute("h", glyph.Height.ToString(CultureInfo.InvariantCulture)),
                        new XAttribute("shift", glyph.Shift.ToString(CultureInfo.InvariantCulture)),
                        new XAttribute("offset", glyph.Offset.ToString(CultureInfo.InvariantCulture))))),
                BuildFontKerningPairsElement(glyphs),
                new XElement("image", font.Name + ".png"));

            SaveXml(fullPath, root);
        }
    }

    private void SerializeGameObjects(string projectDirectory, IEnumerable<GameObject> objects)
    {
        foreach (var gameObject in objects)
        {
            var fullPath = Path.Combine(projectDirectory, "objects", gameObject.Name + ".object.gmx");
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

            var eventsElement = new XElement("events");
            foreach (var gameObjectEvent in gameObject.Events)
            {
                var eventElement = new XElement(
                    "event",
                    new XAttribute("eventtype", ((int)gameObjectEvent.EventType).ToString(CultureInfo.InvariantCulture)));

                if (gameObjectEvent.EventType == GameObjectEventType.Collision)
                {
                    eventElement.SetAttributeValue("ename", GetReferenceName(gameObjectEvent.CollisionObject));
                }
                else
                {
                    eventElement.SetAttributeValue("enumb", gameObjectEvent.EventNumber.ToString(CultureInfo.InvariantCulture));
                }

                WriteGameObjectActions(eventElement, gameObjectEvent.Actions);
                eventsElement.Add(eventElement);
            }

            var root = new XElement("object",
                new XElement("spriteName", GetReferenceName(gameObject.Sprite)),
                new XElement("solid", ToGameMakerBoolean(gameObject.Solid)),
                new XElement("visible", ToGameMakerBoolean(gameObject.Visible)),
                new XElement("depth", gameObject.Depth.ToString(CultureInfo.InvariantCulture)),
                new XElement("persistent", ToGameMakerBoolean(gameObject.Persistent)),
                new XElement("parentName", GetReferenceName(gameObject.Parent)),
                new XElement("maskName", GetReferenceName(gameObject.Mask)),
                eventsElement,
                new XElement("PhysicsObject", ToGameMakerBoolean(gameObject.PhysicsObject)),
                new XElement("PhysicsObjectSensor", ToGameMakerBoolean(gameObject.PhysicsObjectSensor)),
                new XElement("PhysicsObjectShape", gameObject.PhysicsObjectShape.ToString(CultureInfo.InvariantCulture)),
                new XElement("PhysicsObjectDensity", ToInvariantString(gameObject.PhysicsObjectDensity)),
                new XElement("PhysicsObjectRestitution", ToInvariantString(gameObject.PhysicsObjectRestitution)),
                new XElement("PhysicsObjectGroup", gameObject.PhysicsObjectGroup.ToString(CultureInfo.InvariantCulture)),
                new XElement("PhysicsObjectLinearDamping", ToInvariantString(gameObject.PhysicsObjectLinearDamping)),
                new XElement("PhysicsObjectAngularDamping", ToInvariantString(gameObject.PhysicsObjectAngularDamping)),
                new XElement("PhysicsObjectFriction", ToInvariantString(gameObject.PhysicsObjectFriction)),
                new XElement("PhysicsObjectAwake", ToGameMakerBoolean(gameObject.PhysicsObjectAwake)),
                new XElement("PhysicsObjectKinematic", ToGameMakerBoolean(gameObject.PhysicsObjectKinematic)),
                new XElement("PhysicsShapePoints",
                    gameObject.PhysicsShapePoints.Select(static point => new XElement(
                        "point",
                        string.Join(",", ToInvariantString(point.X), ToInvariantString(point.Y))))));

            SaveXml(fullPath, root);
        }
    }

    private void SerializeRooms(string projectDirectory, IEnumerable<Room> rooms)
    {
        foreach (var room in rooms)
        {
            var fullPath = Path.Combine(projectDirectory, "rooms", room.Name + ".room.gmx");
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

            var root = new XElement("room",
                new XElement("caption", room.Caption),
                new XElement("width", room.Width.ToString(CultureInfo.InvariantCulture)),
                new XElement("height", room.Height.ToString(CultureInfo.InvariantCulture)),
                new XElement("vsnap", room.VSnap.ToString(CultureInfo.InvariantCulture)),
                new XElement("hsnap", room.HSnap.ToString(CultureInfo.InvariantCulture)),
                new XElement("isometric", ToGameMakerBoolean(room.Isometric)),
                new XElement("speed", room.Speed.ToString(CultureInfo.InvariantCulture)),
                new XElement("persistent", ToGameMakerBoolean(room.Persistent)),
                new XElement("colour", room.Colour.ToString(CultureInfo.InvariantCulture)),
                new XElement("showcolour", ToGameMakerBoolean(room.ShowColour)),
                new XElement("code", room.Code),
                new XElement("enableViews", ToGameMakerBoolean(room.EnableViews)),
                new XElement("clearViewBackground", ToGameMakerBoolean(room.ViewClearScreen)),
                new XElement("clearDisplayBuffer", ToGameMakerBoolean(room.ClearDisplayBuffer)),
                BuildRoomMakerSettingsElement(room.MakerSettings),
                BuildRoomBackgroundsElement(room.Backgrounds),
                BuildRoomViewsElement(room.Views),
                BuildRoomInstancesElement(room.Instances),
                BuildRoomTilesElement(room.Tiles),
                new XElement("PhysicsWorld", ToGameMakerBoolean(room.PhysicsWorld)),
                new XElement("PhysicsWorldTop", room.PhysicsWorldTop.ToString(CultureInfo.InvariantCulture)),
                new XElement("PhysicsWorldLeft", room.PhysicsWorldLeft.ToString(CultureInfo.InvariantCulture)),
                new XElement("PhysicsWorldRight", room.PhysicsWorldRight.ToString(CultureInfo.InvariantCulture)),
                new XElement("PhysicsWorldBottom", room.PhysicsWorldBottom.ToString(CultureInfo.InvariantCulture)),
                new XElement("PhysicsWorldGravityX", ToInvariantString(room.PhysicsWorldGravityX)),
                new XElement("PhysicsWorldGravityY", ToInvariantString(room.PhysicsWorldGravityY)),
                new XElement("PhysicsWorldPixToMeters", ToInvariantString(room.PhysicsWorldPixToMeters)));

            SaveXml(fullPath, root);
        }
    }

    private void SerializeTimelines(string projectDirectory, IEnumerable<Timeline> timelines)
    {
        foreach (var timeline in timelines)
        {
            var fullPath = Path.Combine(projectDirectory, "timelines", timeline.Name + ".timeline.gmx");
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

            var root = new XElement("timeline");
            foreach (var moment in timeline.Moments.OrderBy(static moment => moment.Step))
            {
                var eventElement = new XElement("event");
                WriteGameObjectActions(eventElement, moment.Actions);

                root.Add(new XElement("entry",
                    new XElement("step", moment.Step.ToString(CultureInfo.InvariantCulture)),
                    eventElement));
            }

            SaveXml(fullPath, root);
        }
    }

    private void SerializeDataFiles(string projectDirectory, IEnumerable<DataFile> dataFiles)
    {
        foreach (var dataFile in dataFiles)
        {
            if (string.IsNullOrWhiteSpace(dataFile.FileName) || dataFile.RawData is null)
            {
                continue;
            }

            var fullPath = Path.Combine(projectDirectory, NormalizeProjectPath(dataFile.FileName));
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            File.WriteAllBytes(fullPath, dataFile.RawData);
        }
    }

    private static void SerializeProjectConfigurationFiles(string projectDirectory, IEnumerable<ProjectConfigurationFile> configurationFiles)
    {
        foreach (var configurationFile in configurationFiles)
        {
            if (string.IsNullOrWhiteSpace(configurationFile.FilePath) || configurationFile.RawData is null)
            {
                continue;
            }

            var fullPath = Path.Combine(projectDirectory, NormalizeProjectPath(configurationFile.FilePath));
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            File.WriteAllBytes(fullPath, configurationFile.RawData);
        }
    }

    private void SerializeExtensions(string projectDirectory, IEnumerable<Extension> extensions)
    {
        foreach (var extension in extensions)
        {
            var fullPath = Path.Combine(projectDirectory, "extensions", extension.Name + ".extension.gmx");
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

            var root = new XElement("extension",
                new XElement("name", extension.Name),
                new XElement("version", extension.Version),
                new XElement("packageID", extension.PackageId),
                new XElement("ProductID", extension.ProductId),
                new XElement("date", extension.Date),
                new XElement("license", extension.License),
                new XElement("description", extension.Description),
                new XElement("helpfile", extension.HelpFile),
                new XElement("installdir", extension.InstallDir),
                new XElement("classname", extension.ClassName),
                new XElement("androidclassname", extension.AndroidClassName),
                new XElement("sourcedir", string.Empty),
                new XElement("androidsourcedir", string.Empty),
                new XElement("macsourcedir", extension.MacSourceDir),
                new XElement("maclinkerflags", extension.MacLinkerFlags),
                new XElement("maccompilerflags", extension.MacCompilerFlags),
                new XElement("androidinject", string.Empty),
                new XElement("androidmanifestinject", string.Empty),
                new XElement("iosplistinject", string.Empty),
                new XElement("androidactivityinject", string.Empty),
                new XElement("gradleinject", string.Empty),
                BuildExtensionFrameworksElement("iosSystemFrameworks", extension.IOSSystemFrameworks),
                BuildExtensionFrameworksElement("iosThirdPartyFrameworks", extension.IOSThirdPartyFrameworks),
                BuildConfigOptionsElement(extension.ConfigOptions),
                new XElement("androidPermissions"),
                BuildExtensionIncludedResourcesElement(extension.IncludedResources),
                BuildExtensionFilesElement(extension));

            SaveXml(fullPath, root);

            foreach (var includedResource in extension.IncludedResources)
            {
                if (includedResource.RawData is null || string.IsNullOrWhiteSpace(includedResource.FilePath))
                {
                    continue;
                }

                var includedResourcePath = Path.Combine(projectDirectory, NormalizeProjectPath(includedResource.FilePath));
                Directory.CreateDirectory(Path.GetDirectoryName(includedResourcePath)!);
                File.WriteAllBytes(includedResourcePath, includedResource.RawData);
            }

            foreach (var packageFile in extension.PackageFiles)
            {
                if (packageFile.RawData is null || string.IsNullOrWhiteSpace(packageFile.RelativePath))
                {
                    continue;
                }

                var packageFilePath = GetExtensionPackageOutputPath(projectDirectory, extension.Name, packageFile.RelativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(packageFilePath)!);
                File.WriteAllBytes(packageFilePath, packageFile.RawData);
            }

            foreach (var include in extension.Includes)
            {
                if (include.RawData is not null && !string.IsNullOrWhiteSpace(include.FileName))
                {
                    var includePath = GetExtensionSidecarOutputPath(projectDirectory, extension.Name, include.FileName);
                    Directory.CreateDirectory(Path.GetDirectoryName(includePath)!);
                    File.WriteAllBytes(includePath, include.RawData);
                }

                foreach (var proxyFile in include.ProxyFiles)
                {
                    if (proxyFile.RawData is null || string.IsNullOrWhiteSpace(proxyFile.Name))
                    {
                        continue;
                    }

                    var proxyPath = GetExtensionSidecarOutputPath(projectDirectory, extension.Name, proxyFile.Name);
                    Directory.CreateDirectory(Path.GetDirectoryName(proxyPath)!);
                    File.WriteAllBytes(proxyPath, proxyFile.RawData);
                }
            }
        }
    }

    private static void SerializeScripts(string projectDirectory, IEnumerable<Script> scripts)
    {
        foreach (var script in scripts)
        {
            var fullPath = Path.Combine(projectDirectory, "scripts", script.Name + ".gml");
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            File.WriteAllText(fullPath, script.SourceCode ?? string.Empty);
        }
    }

    private static void SerializeShaders(string projectDirectory, IEnumerable<Shader> shaders)
    {
        foreach (var shader in shaders)
        {
            var fullPath = Path.Combine(projectDirectory, "shaders", shader.Name + ".shader");
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            File.WriteAllText(fullPath, shader.CombinedSource ?? string.Empty);
        }
    }

    private static void SerializeHelp(string projectDirectory, ProjectHelp help)
    {
        var fileName = string.IsNullOrWhiteSpace(help.RtfFileName)
            ? "help.rtf"
            : help.RtfFileName;

        var fullPath = Path.Combine(projectDirectory, NormalizeProjectPath(fileName));
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, help.Content ?? string.Empty);
    }

    private XElement BuildDataFilesElement(Project project)
    {
        return BuildDataFilesFolderElement(BuildDataFilesTree(project.DataFiles));
    }

    private XElement BuildExtensionsElement(Project project)
    {
        var rootNode = FindProjectResourceRoot(project, ProjectResourceKind.Extension);
        var root = new XElement("NewExtensions");
        var emitted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var index = 0;

        if (rootNode is not null)
        {
            foreach (var childNode in rootNode.Children)
            {
                if (childNode.Resource is not Extension extension)
                {
                    continue;
                }

                emitted.Add(extension.Name);
                root.Add(new XElement(
                    "extension",
                    new XAttribute("index", index++.ToString(CultureInfo.InvariantCulture)),
                    GetProjectEntryPath(ProjectResourceKind.Extension, extension.Name)));
            }
        }

        foreach (var extension in project.Extensions.Where(extension => !emitted.Contains(extension.Name)))
        {
            root.Add(new XElement(
                "extension",
                new XAttribute("index", index++.ToString(CultureInfo.InvariantCulture)),
                GetProjectEntryPath(ProjectResourceKind.Extension, extension.Name)));
        }

        return root;
    }

    private XElement BuildGroupedProjectElement(
        Project project,
        ProjectResourceKind kind,
        string groupElementName,
        string itemElementName,
        string defaultRootName)
    {
        var rootNode = FindProjectResourceRoot(project, kind);
        var resources = GetResourcesByKind(project, kind);
        var root = new XElement(groupElementName);
        root.SetAttributeValue("name", rootNode?.Name ?? defaultRootName);

        var emitted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (rootNode is not null)
        {
            foreach (var childNode in rootNode.Children)
            {
                var childElement = BuildGroupedProjectChildElement(kind, childNode, groupElementName, itemElementName, emitted);
                if (childElement is not null)
                {
                    root.Add(childElement);
                }
            }
        }

        foreach (var resource in resources.Where(resource => !emitted.Contains(resource.Name)))
        {
            root.Add(BuildProjectResourceEntryElement(kind, itemElementName, resource));
        }

        return root;
    }

    private static XElement BuildConfigsElement(IEnumerable<string> configurations)
    {
        var root = new XElement("Configs");
        root.SetAttributeValue("name", "configs");

        foreach (var configuration in configurations)
        {
            root.Add(new XElement("Config", configuration));
        }

        return root;
    }

    private static XElement BuildConstantsElement(IReadOnlyCollection<ProjectConstant> constants)
    {
        var root = new XElement("constants");
        root.SetAttributeValue("number", constants.Count.ToString(CultureInfo.InvariantCulture));

        foreach (var constant in constants)
        {
            root.Add(new XElement(
                "constant",
                new XAttribute("name", constant.Name),
                constant.Value));
        }

        return root;
    }

    private static XElement BuildHelpElement(ProjectHelp help)
    {
        return new XElement(
            "help",
            new XElement("rtf", string.IsNullOrWhiteSpace(help.RtfFileName) ? "help.rtf" : help.RtfFileName));
    }

    private static XElement BuildTutorialStateElement(ProjectTutorialState tutorialState)
    {
        return new XElement("TutorialState",
            new XElement("IsTutorial", ToGameMakerBoolean(tutorialState.IsTutorial)),
            new XElement("TutorialName", tutorialState.TutorialName),
            new XElement("TutorialPage", tutorialState.TutorialPage.ToString(CultureInfo.InvariantCulture)));
    }

    private static XElement BuildConfigOptionsElement(Dictionary<string, long> configOptions)
    {
        var root = new XElement("ConfigOptions");

        foreach (var configOption in configOptions.OrderBy(static pair => pair.Key, StringComparer.OrdinalIgnoreCase))
        {
            root.Add(new XElement("Config",
                new XAttribute("name", configOption.Key),
                new XElement("CopyToMask", configOption.Value.ToString(CultureInfo.InvariantCulture))));
        }

        return root;
    }

    private static XElement BuildIndexedElements(string containerName, string itemPrefix, IEnumerable<int> values)
    {
        var root = new XElement(containerName);
        var index = 0;

        foreach (var value in values)
        {
            root.Add(new XElement(itemPrefix + index++.ToString(CultureInfo.InvariantCulture), value.ToString(CultureInfo.InvariantCulture)));
        }

        if (!root.HasElements)
        {
            root.Add(new XElement(itemPrefix + "0", "0"));
        }

        return root;
    }

    private static XElement BuildFontRangesElement(IReadOnlyCollection<FontRange> ranges, int first, int last)
    {
        var root = new XElement("ranges");
        var orderedRanges = ranges.Count > 0
            ? ranges
            : [new FontRange { Start = first, End = last }];

        var index = 0;
        foreach (var range in orderedRanges)
        {
            root.Add(new XElement(
                "range" + index++.ToString(CultureInfo.InvariantCulture),
                $"{range.Start.ToString(CultureInfo.InvariantCulture)},{range.End.ToString(CultureInfo.InvariantCulture)}"));
        }

        return root;
    }

    private static XElement BuildFontKerningPairsElement(IEnumerable<FontGlyph> glyphs)
    {
        var root = new XElement("kerningPairs");

        foreach (var glyph in glyphs)
        {
            foreach (var kerning in glyph.Kerning)
            {
                root.Add(new XElement("pair",
                    new XAttribute("first", glyph.Character.ToString(CultureInfo.InvariantCulture)),
                    new XAttribute("second", kerning.Other.ToString(CultureInfo.InvariantCulture)),
                    new XAttribute("amount", kerning.Amount.ToString(CultureInfo.InvariantCulture))));
            }
        }

        return root;
    }

    private static XElement BuildRoomMakerSettingsElement(RoomMakerSettings makerSettings)
    {
        return new XElement("makerSettings",
            new XElement("isSet", ToGameMakerBoolean(makerSettings.IsSet)),
            new XElement("w", makerSettings.Width.ToString(CultureInfo.InvariantCulture)),
            new XElement("h", makerSettings.Height.ToString(CultureInfo.InvariantCulture)),
            new XElement("showGrid", ToGameMakerBoolean(makerSettings.ShowGrid)),
            new XElement("showObjects", ToGameMakerBoolean(makerSettings.ShowObjects)),
            new XElement("showTiles", ToGameMakerBoolean(makerSettings.ShowTiles)),
            new XElement("showBackgrounds", ToGameMakerBoolean(makerSettings.ShowBackgrounds)),
            new XElement("showForegrounds", ToGameMakerBoolean(makerSettings.ShowForegrounds)),
            new XElement("showViews", ToGameMakerBoolean(makerSettings.ShowViews)),
            new XElement("deleteUnderlyingObj", ToGameMakerBoolean(makerSettings.DeleteUnderlyingObj)),
            new XElement("deleteUnderlyingTiles", ToGameMakerBoolean(makerSettings.DeleteUnderlyingTiles)),
            new XElement("page", makerSettings.Page.ToString(CultureInfo.InvariantCulture)),
            new XElement("xoffset", makerSettings.XOffset.ToString(CultureInfo.InvariantCulture)),
            new XElement("yoffset", makerSettings.YOffset.ToString(CultureInfo.InvariantCulture)));
    }

    private static XElement BuildRoomBackgroundsElement(IEnumerable<RoomBackground> backgrounds)
    {
        return new XElement("backgrounds",
            backgrounds.Select(static background => new XElement("background",
                new XAttribute("visible", ToGameMakerBoolean(background.Visible)),
                new XAttribute("foreground", ToGameMakerBoolean(background.Foreground)),
                new XAttribute("name", background.Background?.Name ?? string.Empty),
                new XAttribute("x", background.X.ToString(CultureInfo.InvariantCulture)),
                new XAttribute("y", background.Y.ToString(CultureInfo.InvariantCulture)),
                new XAttribute("htiled", ToGameMakerBoolean(background.HTiled)),
                new XAttribute("vtiled", ToGameMakerBoolean(background.VTiled)),
                new XAttribute("hspeed", background.HSpeed.ToString(CultureInfo.InvariantCulture)),
                new XAttribute("vspeed", background.VSpeed.ToString(CultureInfo.InvariantCulture)),
                new XAttribute("stretch", ToGameMakerBoolean(background.Stretch)))));
    }

    private static XElement BuildRoomViewsElement(IEnumerable<RoomView> views)
    {
        return new XElement("views",
            views.Select(static view => new XElement("view",
                new XAttribute("visible", ToGameMakerBoolean(view.Visible)),
                new XAttribute("objName", GetReferenceName(view.FollowObject)),
                new XAttribute("xview", view.XView.ToString(CultureInfo.InvariantCulture)),
                new XAttribute("yview", view.YView.ToString(CultureInfo.InvariantCulture)),
                new XAttribute("wview", view.WView.ToString(CultureInfo.InvariantCulture)),
                new XAttribute("hview", view.HView.ToString(CultureInfo.InvariantCulture)),
                new XAttribute("xport", view.XPort.ToString(CultureInfo.InvariantCulture)),
                new XAttribute("yport", view.YPort.ToString(CultureInfo.InvariantCulture)),
                new XAttribute("wport", view.WPort.ToString(CultureInfo.InvariantCulture)),
                new XAttribute("hport", view.HPort.ToString(CultureInfo.InvariantCulture)),
                new XAttribute("hborder", view.HBorder.ToString(CultureInfo.InvariantCulture)),
                new XAttribute("vborder", view.VBorder.ToString(CultureInfo.InvariantCulture)),
                new XAttribute("hspeed", view.HSpeed.ToString(CultureInfo.InvariantCulture)),
                new XAttribute("vspeed", view.VSpeed.ToString(CultureInfo.InvariantCulture)))));
    }

    private static XElement BuildRoomInstancesElement(IEnumerable<RoomInstance> instances)
    {
        return new XElement("instances",
            instances.Select(static instance => new XElement("instance",
                new XAttribute("objName", GetReferenceName(instance.Object)),
                new XAttribute("x", instance.X.ToString(CultureInfo.InvariantCulture)),
                new XAttribute("y", instance.Y.ToString(CultureInfo.InvariantCulture)),
                new XAttribute("name", instance.Name),
                new XAttribute("locked", "0"),
                new XAttribute("code", instance.Code),
                new XAttribute("scaleX", ToInvariantString(instance.ScaleX)),
                new XAttribute("scaleY", ToInvariantString(instance.ScaleY)),
                new XAttribute("colour", instance.Colour.ToString(CultureInfo.InvariantCulture)),
                new XAttribute("rotation", ToInvariantString(instance.Rotation)),
                new XAttribute("id", instance.Id.ToString(CultureInfo.InvariantCulture)))));
    }

    private static XElement BuildRoomTilesElement(IEnumerable<RoomTile> tiles)
    {
        return new XElement("tiles",
            tiles.Select(static tile => new XElement("tile",
                new XAttribute("bgName", tile.Background?.Name ?? string.Empty),
                new XAttribute("x", tile.X.ToString(CultureInfo.InvariantCulture)),
                new XAttribute("y", tile.Y.ToString(CultureInfo.InvariantCulture)),
                new XAttribute("w", tile.Width.ToString(CultureInfo.InvariantCulture)),
                new XAttribute("h", tile.Height.ToString(CultureInfo.InvariantCulture)),
                new XAttribute("xo", tile.SourceX.ToString(CultureInfo.InvariantCulture)),
                new XAttribute("yo", tile.SourceY.ToString(CultureInfo.InvariantCulture)),
                new XAttribute("id", tile.Id.ToString(CultureInfo.InvariantCulture)),
                new XAttribute("name", "tile_" + tile.Id.ToString(CultureInfo.InvariantCulture)),
                new XAttribute("depth", tile.Depth.ToString(CultureInfo.InvariantCulture)),
                new XAttribute("locked", "0"),
                new XAttribute("colour", ConvertRgbToGameMakerColour(tile.Blend, tile.Alpha).ToString(CultureInfo.InvariantCulture)),
                new XAttribute("scaleX", ToInvariantString(tile.ScaleX)),
                new XAttribute("scaleY", ToInvariantString(tile.ScaleY)))));
    }

    private static XElement BuildExtensionFrameworksElement(string elementName, IEnumerable<ExtensionFramework> frameworks)
    {
        var root = new XElement(elementName);

        foreach (var framework in frameworks)
        {
            var frameworkElement = new XElement("framework", framework.Name);
            if (framework.WeakReference)
            {
                frameworkElement.SetAttributeValue("weak", "1");
            }

            root.Add(frameworkElement);
        }

        return root;
    }

    private static XElement BuildExtensionIncludedResourcesElement(IEnumerable<ExtensionIncludedResource> includedResources)
    {
        var root = new XElement("IncludedResources");

        foreach (var includedResource in includedResources)
        {
            if (string.IsNullOrWhiteSpace(includedResource.FilePath))
            {
                continue;
            }

            root.Add(new XElement("Resource", ToProjectPath(includedResource.FilePath)));
        }

        return root;
    }

    private static XElement BuildExtensionFilesElement(Extension extension)
    {
        var root = new XElement("files");

        foreach (var include in extension.Includes)
        {
            var fileElement = new XElement("file",
                new XElement("filename", include.FileName),
                new XElement("origname", string.IsNullOrWhiteSpace(include.OriginalName)
                    ? ToProjectPath(Path.Combine("extensions", include.FileName))
                    : ToProjectPath(include.OriginalName)),
                new XElement("init", include.Init),
                new XElement("final", include.Final),
                new XElement("kind", include.Kind.ToString(CultureInfo.InvariantCulture)),
                new XElement("uncompress", ToGameMakerBoolean(include.Uncompress)),
                BuildConfigOptionsElement(include.ConfigOptions),
                BuildExtensionProxyFilesElement(include.ProxyFiles),
                BuildExtensionFunctionsElement(include.Functions),
                BuildExtensionConstantsElement(include.Constants));

            root.Add(fileElement);
        }

        return root;
    }

    private static string GetExtensionSidecarOutputPath(string projectDirectory, string extensionName, string fileName)
    {
        return Path.Combine(projectDirectory, "extensions", extensionName, NormalizeProjectPath(fileName));
    }

    private static string GetExtensionPackageOutputPath(string projectDirectory, string extensionName, string relativePath)
    {
        return Path.Combine(projectDirectory, "extensions", extensionName, NormalizeProjectPath(relativePath));
    }

    private static XElement BuildExtensionProxyFilesElement(IEnumerable<ExtensionProxyFile> proxyFiles)
    {
        var root = new XElement("ProxyFiles");

        foreach (var proxyFile in proxyFiles)
        {
            root.Add(new XElement("ProxyFile",
                new XElement("Name", proxyFile.Name),
                new XElement("TargetMask", proxyFile.TargetMask.ToString(CultureInfo.InvariantCulture))));
        }

        return root;
    }

    private static XElement BuildExtensionFunctionsElement(IEnumerable<ExtensionFunction> functions)
    {
        var root = new XElement("functions");

        foreach (var function in functions)
        {
            root.Add(new XElement("function",
                new XElement("name", function.Name),
                new XElement("externalName", function.ExternalName),
                new XElement("kind", function.Kind.ToString(CultureInfo.InvariantCulture)),
                new XElement("help", function.Help),
                new XElement("returnType", function.ReturnType.ToString(CultureInfo.InvariantCulture)),
                new XElement("argCount", function.ArgCount.ToString(CultureInfo.InvariantCulture)),
                new XElement("args",
                    function.Args.Select(static argument => new XElement("arg", argument.ToString(CultureInfo.InvariantCulture))))));
        }

        return root;
    }

    private static XElement BuildExtensionConstantsElement(IEnumerable<ExtensionConstant> constants)
    {
        var root = new XElement("constants");

        foreach (var constant in constants)
        {
            root.Add(new XElement("constant",
                new XElement("name", constant.Name),
                new XElement("value", constant.Value)));
        }

        return root;
    }

    private static void WriteGameObjectActions(XElement parentElement, IEnumerable<GameObjectAction> actions)
    {
        foreach (var action in actions)
        {
            var actionElement = new XElement("action",
                new XElement("libid", action.LibId.ToString(CultureInfo.InvariantCulture)),
                new XElement("id", action.Id.ToString(CultureInfo.InvariantCulture)),
                new XElement("kind", ((int)action.Kind).ToString(CultureInfo.InvariantCulture)),
                new XElement("userelative", ToGameMakerBoolean(action.UseRelative)),
                new XElement("isquestion", ToGameMakerBoolean(action.IsQuestion)),
                new XElement("useapplyto", ToGameMakerBoolean(action.UseApplyTo)),
                new XElement("exetype", ((int)action.ExecuteType).ToString(CultureInfo.InvariantCulture)),
                new XElement("functionname", action.FunctionName),
                new XElement("codestring", action.CodeString),
                new XElement("whoName", action.WhoName),
                new XElement("relative", ToGameMakerBoolean(action.Relative)),
                new XElement("isnot", ToGameMakerBoolean(action.IsNot)));

            var argumentsElement = new XElement("arguments");
            foreach (var argument in action.Arguments)
            {
                argumentsElement.Add(new XElement("argument",
                    new XElement("kind", ((int)argument.Kind).ToString(CultureInfo.InvariantCulture)),
                    new XElement(GetActionArgumentElementName(argument.Kind), argument.Value)));
            }

            actionElement.Add(argumentsElement);
            parentElement.Add(actionElement);
        }
    }

    private static ProjectResourceTreeNode? FindProjectResourceRoot(Project project, ProjectResourceKind kind)
    {
        return project.ResourceTree.FirstOrDefault(node => node.Kind == kind);
    }

    private static IEnumerable<Resource> GetResourcesByKind(Project project, ProjectResourceKind kind)
    {
        return kind switch
        {
            ProjectResourceKind.Sprite => project.Sprites,
            ProjectResourceKind.Sound => project.Sounds,
            ProjectResourceKind.Background => project.Backgrounds,
            ProjectResourceKind.Path => project.Paths,
            ProjectResourceKind.Script => project.Scripts,
            ProjectResourceKind.Shader => project.Shaders,
            ProjectResourceKind.Font => project.Fonts,
            ProjectResourceKind.Object => project.Objects,
            ProjectResourceKind.Timeline => project.Timelines,
            ProjectResourceKind.Room => project.Rooms,
            ProjectResourceKind.DataFile => project.DataFiles,
            ProjectResourceKind.Extension => project.Extensions,
            _ => [],
        };
    }

    private static XElement? BuildGroupedProjectChildElement(
        ProjectResourceKind kind,
        ProjectResourceTreeNode node,
        string groupElementName,
        string itemElementName,
        HashSet<string> emitted)
    {
        if (!node.IsFolder)
        {
            if (node.Resource is null)
            {
                return null;
            }

            emitted.Add(node.Resource.Name);
            return BuildProjectResourceEntryElement(kind, itemElementName, node.Resource);
        }

        var groupElement = new XElement(groupElementName);
        groupElement.SetAttributeValue("name", node.Name);

        foreach (var childNode in node.Children)
        {
            var childElement = BuildGroupedProjectChildElement(kind, childNode, groupElementName, itemElementName, emitted);
            if (childElement is not null)
            {
                groupElement.Add(childElement);
            }
        }

        return groupElement;
    }

    private static XElement BuildProjectResourceEntryElement(ProjectResourceKind kind, string itemElementName, Resource resource)
    {
        var element = new XElement(itemElementName, GetProjectEntryPath(kind, resource.Name));

        if (kind == ProjectResourceKind.Shader
            && resource is Shader shader
            && !string.IsNullOrWhiteSpace(shader.ProjectType))
        {
            element.SetAttributeValue("type", shader.ProjectType);
        }

        return element;
    }

    private static ProjectResourceTreeNode BuildDataFilesTree(IEnumerable<DataFile> dataFiles)
    {
        var root = new ProjectResourceTreeNode
        {
            Name = "datafiles",
            Kind = ProjectResourceKind.DataFile,
        };

        foreach (var dataFile in dataFiles)
        {
            var normalizedPath = NormalizeProjectPath(dataFile.FileName);
            var segments = normalizedPath.Split('\\', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length == 0)
            {
                continue;
            }

            var currentNode = root;
            var startIndex = string.Equals(segments[0], "datafiles", StringComparison.OrdinalIgnoreCase) ? 1 : 0;

            for (var index = startIndex; index < segments.Length - 1; index++)
            {
                var segment = segments[index];
                var childNode = currentNode.Children.FirstOrDefault(child => child.IsFolder && string.Equals(child.Name, segment, StringComparison.OrdinalIgnoreCase));
                if (childNode is null)
                {
                    childNode = new ProjectResourceTreeNode
                    {
                        Name = segment,
                        Kind = ProjectResourceKind.DataFile,
                    };

                    currentNode.Children.Add(childNode);
                }

                currentNode = childNode;
            }

            currentNode.Children.Add(new ProjectResourceTreeNode
            {
                Name = string.IsNullOrWhiteSpace(dataFile.Name) ? segments[^1] : dataFile.Name,
                Kind = ProjectResourceKind.DataFile,
                Resource = dataFile,
            });
        }

        return root;
    }

    private static XElement BuildDataFilesFolderElement(ProjectResourceTreeNode node)
    {
        var globalNumber = CountDataFiles(node) + 1;
        return BuildDataFilesFolderElement(node, globalNumber);
    }

    private static XElement BuildDataFilesFolderElement(ProjectResourceTreeNode node, int globalNumber)
    {
        var element = new XElement("datafiles");
        element.SetAttributeValue("name", node.Name);
        element.SetAttributeValue("number", globalNumber.ToString(CultureInfo.InvariantCulture));

        foreach (var childNode in node.Children)
        {
            if (childNode.IsFolder)
            {
                element.Add(BuildDataFilesFolderElement(childNode, globalNumber));
                continue;
            }

            if (childNode.Resource is not DataFile dataFile)
            {
                continue;
            }

            element.Add(new XElement("datafile",
                new XElement("name", string.IsNullOrWhiteSpace(dataFile.Name) ? Path.GetFileName(dataFile.FileName) : dataFile.Name),
                new XElement("exists", ToGameMakerBoolean(dataFile.Exists)),
                new XElement("size", (dataFile.Size == 0 && dataFile.RawData is not null ? dataFile.RawData.Length : dataFile.Size).ToString(CultureInfo.InvariantCulture)),
                new XElement("exportAction", dataFile.ExportAction.ToString(CultureInfo.InvariantCulture)),
                new XElement("exportDir", dataFile.ExportDir),
                new XElement("overwrite", ToGameMakerBoolean(dataFile.Overwrite)),
                new XElement("freeData", ToGameMakerBoolean(dataFile.FreeData)),
                new XElement("removeEnd", ToGameMakerBoolean(dataFile.RemoveEnd)),
                new XElement("store", ToGameMakerBoolean(dataFile.Store)),
                BuildConfigOptionsElement(dataFile.ConfigOptions),
                new XElement("filename", Path.GetFileName(dataFile.FileName))));
        }

        return element;
    }

    private static int CountDataFiles(ProjectResourceTreeNode node)
    {
        var count = 0;

        foreach (var childNode in node.Children)
        {
            if (childNode.IsFolder)
            {
                count += CountDataFiles(childNode);
            }
            else if (childNode.Resource is DataFile)
            {
                count++;
            }
        }

        return count;
    }

    private static string GetProjectEntryPath(ProjectResourceKind kind, string resourceName)
    {
        return kind switch
        {
            ProjectResourceKind.Sprite => ToProjectPath(Path.Combine("sprites", resourceName)),
            ProjectResourceKind.Sound => ToProjectPath(Path.Combine("sound", resourceName)),
            ProjectResourceKind.Background => ToProjectPath(Path.Combine("background", resourceName)),
            ProjectResourceKind.Path => ToProjectPath(Path.Combine("paths", resourceName)),
            ProjectResourceKind.Script => ToProjectPath(Path.Combine("scripts", resourceName + ".gml")),
            ProjectResourceKind.Shader => ToProjectPath(Path.Combine("shaders", resourceName + ".shader")),
            ProjectResourceKind.Font => ToProjectPath(Path.Combine("fonts", resourceName)),
            ProjectResourceKind.Object => ToProjectPath(Path.Combine("objects", resourceName)),
            ProjectResourceKind.Timeline => ToProjectPath(Path.Combine("timelines", resourceName)),
            ProjectResourceKind.Room => ToProjectPath(Path.Combine("rooms", resourceName)),
            ProjectResourceKind.Extension => ToProjectPath(Path.Combine("extensions", resourceName)),
            _ => resourceName,
        };
    }

    private static void SaveXml(string path, XElement root)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        var document = new XDocument(
            new XComment("This Document is generated by GameMaker, if you edit it by hand then you do so at your own risk!"),
            root);

        File.WriteAllText(path, document.ToString() + Environment.NewLine);
    }

    private static void SaveBitmap(Bitmap bitmap, string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        bitmap.Save(path, 100);
    }

    private static string ToInvariantString(float value) => value.ToString("R", CultureInfo.InvariantCulture);

    private static string ToInvariantString(double value) => value.ToString("R", CultureInfo.InvariantCulture);

    private static string ToProjectPath(string path) => NormalizeProjectPath(path);

    private static string ToGameMakerBoolean(bool value) => value ? "-1" : "0";

    private static string GetReferenceName(Resource? resource) => resource?.Name ?? "<undefined>";

    private static string GetActionArgumentElementName(GameObjectActionArgumentKind kind)
    {
        return kind switch
        {
            GameObjectActionArgumentKind.Boolean => "boolean",
            GameObjectActionArgumentKind.Menu => "menu",
            GameObjectActionArgumentKind.Sprite => "sprite",
            GameObjectActionArgumentKind.Sound => "sound",
            GameObjectActionArgumentKind.Background => "background",
            GameObjectActionArgumentKind.Path => "path",
            GameObjectActionArgumentKind.Script => "script",
            GameObjectActionArgumentKind.Object => "object",
            GameObjectActionArgumentKind.Room => "room",
            GameObjectActionArgumentKind.FontReference => "font",
            GameObjectActionArgumentKind.Color => "color",
            GameObjectActionArgumentKind.Timeline => "timeline",
            GameObjectActionArgumentKind.Font => "font",
            _ => "string",
        };
    }

    private static string ResolveSoundExtension(Sound sound)
    {
        if (!string.IsNullOrWhiteSpace(sound.Extension))
        {
            return sound.Extension.StartsWith(".", StringComparison.Ordinal)
                ? sound.Extension
                : "." + sound.Extension;
        }

        if (!string.IsNullOrWhiteSpace(sound.OriginalName))
        {
            var originalExtension = Path.GetExtension(sound.OriginalName);
            if (!string.IsNullOrWhiteSpace(originalExtension))
            {
                return originalExtension;
            }
        }

        return ".wav";
    }

    private static int GetSpriteWidth(Sprite sprite)
    {
        return sprite.Width != 0
            ? sprite.Width
            : sprite.Frames.Select(static frame => frame.Width).FirstOrDefault(width => width > 0);
    }

    private static int GetSpriteHeight(Sprite sprite)
    {
        return sprite.Height != 0
            ? sprite.Height
            : sprite.Frames.Select(static frame => frame.Height).FirstOrDefault(height => height > 0);
    }

    private static int GetBackgroundWidth(Background background) => background.Width != 0 ? background.Width : background.Bitmap?.PixelSize.Width ?? 0;

    private static int GetBackgroundHeight(Background background) => background.Height != 0 ? background.Height : background.Bitmap?.PixelSize.Height ?? 0;

    private static int MapCompressionQualityToBitRate(int compressionQuality)
    {
        return compressionQuality switch
        {
            <= 0 => 48,
            1 => 64,
            2 => 96,
            3 => 112,
            4 => 128,
            5 => 160,
            6 => 192,
            7 => 224,
            8 => 256,
            9 => 320,
            _ => 512,
        };
    }

    private static uint ConvertRgbToGameMakerColour(int rgb, double alpha)
    {
        var clampedAlpha = Math.Clamp(alpha, 0.0, 1.0);
        var alphaByte = (uint)Math.Round(clampedAlpha * byte.MaxValue, MidpointRounding.AwayFromZero);
        var red = (uint)(rgb & 0xFF);
        var green = (uint)((rgb >> 8) & 0xFF);
        var blue = (uint)((rgb >> 16) & 0xFF);

        return (alphaByte << 24) | (red << 16) | (green << 8) | blue;
    }
}
