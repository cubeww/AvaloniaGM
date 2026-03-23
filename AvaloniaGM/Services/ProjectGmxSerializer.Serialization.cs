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
}
