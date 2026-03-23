using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Media;
using AvaloniaGM.Models;
using Avalonia.Svg.Skia;

namespace AvaloniaGM.Services
{
    internal static class AppIconCatalog
    {
        private const string TreeIconBasePath = "avares://AvaloniaGM/Assets/Icons/Fluent/Tree/";
        private static readonly Dictionary<string, IImage> Cache = new(StringComparer.OrdinalIgnoreCase);

        public static IImage GetTreeIcon(ProjectResourceKind kind, bool isFolder, bool isExpanded)
        {
            if (isFolder)
            {
                return GetSvgImage(isExpanded ? "folder-open.svg" : "folder.svg");
            }

            return kind switch
            {
                ProjectResourceKind.Sprite => GetSvgImage("image.svg"),
                ProjectResourceKind.Sound => GetSvgImage("music-note.svg"),
                ProjectResourceKind.Background => GetSvgImage("image.svg"),
                ProjectResourceKind.Path => GetSvgImage("flow.svg"),
                ProjectResourceKind.Script => GetSvgImage("script.svg"),
                ProjectResourceKind.Shader => GetSvgImage("code.svg"),
                ProjectResourceKind.Font => GetSvgImage("text-font.svg"),
                ProjectResourceKind.Object => GetSvgImage("cube.svg"),
                ProjectResourceKind.Timeline => GetSvgImage("grid.svg"),
                ProjectResourceKind.Room => GetSvgImage("window-text.svg"),
                ProjectResourceKind.DataFile => GetSvgImage("document.svg"),
                ProjectResourceKind.Extension => GetSvgImage("puzzle-piece.svg"),
                _ => GetSvgImage("document.svg"),
            };
        }

        private static IImage GetSvgImage(string fileName)
        {
            var assetUri = TreeIconBasePath + fileName;
            if (Cache.TryGetValue(assetUri, out var image))
            {
                return image;
            }

            image = new SvgImage
            {
                Source = SvgSource.Load(assetUri, baseUri: null)
            };

            Cache[assetUri] = image;
            return image;
        }
    }
}
