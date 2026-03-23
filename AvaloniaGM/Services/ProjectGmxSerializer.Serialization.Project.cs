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
}
