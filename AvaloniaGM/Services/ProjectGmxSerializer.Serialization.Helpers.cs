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
