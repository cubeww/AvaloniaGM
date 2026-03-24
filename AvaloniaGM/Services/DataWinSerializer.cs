using System;
using System.Collections.Generic;
using System.Globalization;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Avalonia.Media.Imaging;
using GM = AvaloniaGM.Models;
using UndertaleModLib;
using UndertaleModLib.Compiler;
using UndertaleModLib.Models;
using UndertaleModLib.Util;

namespace AvaloniaGM.Services;

public class DataWinSerializer
{
    private const uint Gms1Major = 1;
    private const uint Gms1Minor = 4;
    private const uint Gms1Release = 0;
    private const uint Gms1Build = 1804;
    private const byte Gms1BytecodeVersion = 16;
    private const string DefaultConfiguration = "Default";
    private const string DefaultAudioGroupName = "audiogroup_default";

    public void SerializeProject(string dataWinPath, GM.Project project)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataWinPath);
        ArgumentNullException.ThrowIfNull(project);

        var fullPath = Path.GetFullPath(dataWinPath);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using var data = BuildData(project);
        using var stream = File.Create(fullPath);
        UndertaleIO.Write(stream, data);
    }

    private static UndertaleData BuildData(GM.Project project)
    {
        var data = UndertaleData.CreateNew();
        ConfigureGeneralInfo(data, project);
        ResetDefaultResources(data);
        AddProjectConstants(data, project);

        var defaultAudioGroup = CreateDefaultAudioGroup(data);
        var spriteMap = CreateSprites(data, project.Sprites);
        CreateSounds(data, project.Sounds, defaultAudioGroup);
        var backgroundMap = CreateBackgrounds(data, project.Backgrounds);
        CreatePaths(data, project.Paths);
        var scriptCodeMap = CreateScripts(data, project.Scripts);
        CreateShaders(data, project.Shaders);
        CreateFonts(data, project.Fonts);
        var objectMap = CreateObjectShells(data, project.Objects);
        PopulateObjects(project.Objects, objectMap, spriteMap);
        var extensionScripts = CreateExtensions(data, project.Extensions);
        CreateTimelines(data, project.Timelines);
        CreateRooms(data, project.Rooms, backgroundMap, objectMap);

        var importer = new CodeImportGroup(data)
        {
            AutoCreateAssets = false,
        };

        QueueScriptCompilation(importer, project.Scripts, scriptCodeMap);
        QueueExtensionScriptCompilation(importer, extensionScripts);
        QueueObjectCompilation(data, importer, project.Objects, objectMap);
        QueueTimelineCompilation(importer, project.Timelines, data.Timelines);
        QueueRoomCompilation(importer, project.Rooms, data.Rooms);
        importer.Import();

        UpdateGeneralInfoCounters(data);
        return data;
    }

    private static void ConfigureGeneralInfo(UndertaleData data, GM.Project project)
    {
        var configurationName = project.Configurations.FirstOrDefault(static configuration => !string.IsNullOrWhiteSpace(configuration))
            ?? DefaultConfiguration;
        var displayName = string.IsNullOrWhiteSpace(project.Name)
            ? "AvaloniaGM Project"
            : project.Name;

        data.GeneralInfo.BytecodeVersion = Gms1BytecodeVersion;
        data.GeneralInfo.Major = Gms1Major;
        data.GeneralInfo.Minor = Gms1Minor;
        data.GeneralInfo.Release = Gms1Release;
        data.GeneralInfo.Build = Gms1Build;
        data.GeneralInfo.Name = data.Strings.MakeString(displayName);
        data.GeneralInfo.FileName = data.Strings.MakeString(displayName);
        data.GeneralInfo.DisplayName = data.Strings.MakeString(displayName);
        data.GeneralInfo.Config = data.Strings.MakeString(configurationName);
        var (defaultWindowWidth, defaultWindowHeight) = GetFirstRoomWindowSize(project);
        data.GeneralInfo.DefaultWindowWidth = defaultWindowWidth;
        data.GeneralInfo.DefaultWindowHeight = defaultWindowHeight;

        data.Options.Info |= UndertaleOptions.OptionsFlags.CreationEventOrder;
    }

    private static (uint Width, uint Height) GetFirstRoomWindowSize(GM.Project project)
    {
        var room = project.Rooms.FirstOrDefault();
        if (room is null)
        {
            return (1024, 768);
        }

        var minX = 0;
        var minY = 0;
        var maxX = room.Width;
        var maxY = room.Height;
        var firstVisibleView = true;

        if (room.EnableViews)
        {
            foreach (var view in room.Views)
            {
                if (!view.Visible)
                {
                    continue;
                }

                if (firstVisibleView)
                {
                    minX = view.XPort;
                    minY = view.YPort;
                    maxX = view.XPort + view.WPort;
                    maxY = view.YPort + view.HPort;
                    firstVisibleView = false;
                    continue;
                }

                minX = Math.Min(minX, view.XPort);
                minY = Math.Min(minY, view.YPort);
                maxX = Math.Max(maxX, view.XPort + view.WPort);
                maxY = Math.Max(maxY, view.YPort + view.HPort);
            }
        }

        return ((uint)Math.Max(1, maxX - minX), (uint)Math.Max(1, maxY - minY));
    }

    private static void ResetDefaultResources(UndertaleData data)
    {
        data.Rooms.Clear();
        data.GeneralInfo.RoomOrder.Clear();
    }

    private static void AddProjectConstants(UndertaleData data, GM.Project project)
    {
        foreach (var constant in project.Constants)
        {
            if (string.IsNullOrWhiteSpace(constant.Name))
            {
                continue;
            }

            data.Options.Constants.Add(new UndertaleOptions.Constant
            {
                Name = data.Strings.MakeString(constant.Name),
                Value = data.Strings.MakeString(constant.Value ?? string.Empty),
            });
        }
    }

    private static UndertaleAudioGroup CreateDefaultAudioGroup(UndertaleData data)
    {
        var audioGroup = new UndertaleAudioGroup
        {
            Name = data.Strings.MakeString(DefaultAudioGroupName),
        };
        data.AudioGroups.Add(audioGroup);
        return audioGroup;
    }

    private static Dictionary<GM.Sprite, UndertaleSprite> CreateSprites(UndertaleData data, IEnumerable<GM.Sprite> sprites)
    {
        var map = new Dictionary<GM.Sprite, UndertaleSprite>();

        foreach (var sprite in sprites)
        {
            var size = GetSpriteSize(sprite);
            var orderedFrames = sprite.Frames
                .OrderBy(static frame => frame.Index)
                .ToList();
            var frameImages = orderedFrames
                .Select(frame => CreateMaskSourceImage(frame.Bitmap))
                .ToList();
            var collisionBounds = GetSpriteCollisionBounds(sprite, size.Width, size.Height, frameImages);
            var undertaleSprite = new UndertaleSprite
            {
                Name = data.Strings.MakeString(sprite.Name),
                Width = (uint)Math.Max(0, size.Width),
                Height = (uint)Math.Max(0, size.Height),
                Transparent = true,
                Smooth = false,
                Preload = true,
                BBoxMode = (uint)sprite.BoundingBoxMode,
                SepMasks = MapCollisionKind(sprite.CollisionKind),
                OriginX = sprite.XOrigin,
                OriginY = sprite.YOrigin,
            };

            ApplySpriteMargins(undertaleSprite, size.Width, size.Height, collisionBounds);

            foreach (var frame in orderedFrames)
            {
                var texturePageItem = CreateTexturePageItem(data, $"{sprite.Name}_{frame.Index}", frame.Bitmap, size.Width, size.Height);
                if (texturePageItem is null)
                {
                    continue;
                }

                undertaleSprite.Textures.Add(new UndertaleSprite.TextureEntry
                {
                    Texture = texturePageItem,
                });
            }

            var maskCount = undertaleSprite.Textures.Count == 0
                ? 0
                : (sprite.SeparateCollisionMasks || sprite.CollisionKind == GM.SpriteCollisionKind.PrecisePerFrame
                    ? undertaleSprite.Textures.Count
                    : 1);

            for (var index = 0; index < maskCount; index++)
            {
                var maskEntry = undertaleSprite.NewMaskEntry(data);
                PopulateSpriteMask(maskEntry, sprite, size.Width, size.Height, collisionBounds, frameImages, index, maskCount);
                undertaleSprite.CollisionMasks.Add(maskEntry);
            }

            data.Sprites.Add(undertaleSprite);
            map[sprite] = undertaleSprite;
        }

        return map;
    }

    private static void CreateSounds(UndertaleData data, IEnumerable<GM.Sound> sounds, UndertaleAudioGroup defaultAudioGroup)
    {
        foreach (var sound in sounds)
        {
            var extension = NormalizeSoundExtension(sound.Extension, sound.OriginalName);
            UndertaleEmbeddedAudio? embeddedAudio = null;
            if (!sound.Streamed)
            {
                embeddedAudio = new UndertaleEmbeddedAudio
                {
                    Name = data.Strings.MakeString(sound.Name),
                    Data = sound.RawData ?? Array.Empty<byte>(),
                };
                data.EmbeddedAudio.Add(embeddedAudio);
            }

            var soundEntry = new UndertaleSound
            {
                Name = data.Strings.MakeString(sound.Name),
                Type = data.Strings.MakeString(extension),
                File = data.Strings.MakeString(sound.Streamed ? GetExternalSoundFileName(sound) : GetSoundFileName(sound, extension)),
                Effects = (uint)Math.Max(0, sound.Effects),
                Volume = (float)sound.Volume,
                Pitch = (float)sound.Pan,
                Preload = sound.Preload,
                AudioGroup = defaultAudioGroup,
                Flags = BuildSoundFlags(sound),
            };

            if (embeddedAudio is not null)
            {
                soundEntry.AudioFile = embeddedAudio;
            }
            else
            {
                soundEntry.AudioID = -1;
            }

            data.Sounds.Add(soundEntry);
        }
    }

    private static Dictionary<GM.Background, UndertaleBackground> CreateBackgrounds(UndertaleData data, IEnumerable<GM.Background> backgrounds)
    {
        var map = new Dictionary<GM.Background, UndertaleBackground>();

        foreach (var background in backgrounds)
        {
            var width = Math.Max(1, background.Width);
            var height = Math.Max(1, background.Height);
            var undertaleBackground = new UndertaleBackground
            {
                Name = data.Strings.MakeString(background.Name),
                Transparent = true,
                Smooth = false,
                Preload = true,
                Texture = CreateTexturePageItem(data, background.Name, background.Bitmap, width, height),
            };

            data.Backgrounds.Add(undertaleBackground);
            map[background] = undertaleBackground;
        }

        return map;
    }

    private static void CreatePaths(UndertaleData data, IEnumerable<GM.GamePath> paths)
    {
        foreach (var path in paths)
        {
            var undertalePath = new UndertalePath
            {
                Name = data.Strings.MakeString(path.Name),
                IsSmooth = path.Kind != 0,
                IsClosed = path.Closed,
                Precision = (uint)Math.Max(1, path.Precision),
            };

            foreach (var point in path.Points)
            {
                undertalePath.Points.Add(new UndertalePath.PathPoint
                {
                    X = (float)point.X,
                    Y = (float)point.Y,
                    Speed = (float)point.Speed,
                });
            }

            data.Paths.Add(undertalePath);
        }
    }

    private static Dictionary<GM.Script, UndertaleCode> CreateScripts(UndertaleData data, IEnumerable<GM.Script> scripts)
    {
        var map = new Dictionary<GM.Script, UndertaleCode>();

        foreach (var script in scripts)
        {
            var code = UndertaleCode.CreateEmptyEntry(data, GetScriptCodeName(script.Name));
            var scriptEntry = new UndertaleScript
            {
                Name = data.Strings.MakeString(script.Name),
                Code = code,
            };

            data.Scripts.Add(scriptEntry);
            map[script] = code;
        }

        return map;
    }

    private static void CreateShaders(UndertaleData data, IEnumerable<GM.Shader> shaders)
    {
        foreach (var shader in shaders)
        {
            var shaderType = MapShaderType(shader.ProjectType);
            var vertexSource = shader.VertexSource ?? string.Empty;
            var fragmentSource = shader.FragmentSource ?? string.Empty;
            var shaderTexts = BuildShaderTexts(shaderType, shader.Name, vertexSource, fragmentSource);
            var shaderEntry = new UndertaleShader
            {
                Name = data.Strings.MakeString(shader.Name),
                Type = shaderType,
                GLSL_ES_Vertex = data.Strings.MakeString(shaderTexts.GlslEsVertex),
                GLSL_ES_Fragment = data.Strings.MakeString(shaderTexts.GlslEsFragment),
                GLSL_Vertex = data.Strings.MakeString(shaderTexts.GlslVertex),
                GLSL_Fragment = data.Strings.MakeString(shaderTexts.GlslFragment),
                HLSL9_Vertex = data.Strings.MakeString(shaderTexts.Hlsl9Vertex),
                HLSL9_Fragment = data.Strings.MakeString(shaderTexts.Hlsl9Fragment),
            };

            PopulateCompiledShaderData(shaderEntry, shaderType, shader.Name, vertexSource, fragmentSource);

            foreach (var attributeName in EnumerateShaderAttributes(vertexSource))
            {
                shaderEntry.VertexShaderAttributes.Add(new UndertaleShader.VertexShaderAttribute
                {
                    Name = data.Strings.MakeString(attributeName),
                });
            }

            data.Shaders.Add(shaderEntry);
        }
    }

    private static void CreateFonts(UndertaleData data, IEnumerable<GM.Font> fonts)
    {
        foreach (var font in fonts)
        {
            var atlasWidth = font.Bitmap?.PixelSize.Width
                ?? Math.Max(1, font.Glyphs.DefaultIfEmpty().Max(static glyph => glyph?.X + glyph?.Width ?? 1));
            var atlasHeight = font.Bitmap?.PixelSize.Height
                ?? Math.Max(1, font.Glyphs.DefaultIfEmpty().Max(static glyph => glyph?.Y + glyph?.Height ?? 1));
            var texturePageItem = CreateTexturePageItem(data, font.Name, font.Bitmap, atlasWidth, atlasHeight);
            var firstChar = font.Glyphs.Count > 0 ? font.Glyphs.Min(static glyph => glyph.Character) : font.First;
            var lastChar = font.Glyphs.Count > 0 ? font.Glyphs.Max(static glyph => glyph.Character) : font.Last;
            var fontEntry = new UndertaleFont
            {
                Name = data.Strings.MakeString(font.Name),
                DisplayName = data.Strings.MakeString(string.IsNullOrWhiteSpace(font.FontName) ? font.Name : font.FontName),
                EmSize = MathF.Round(font.Size),
                EmSizeIsFloat = false,
                Bold = font.Bold,
                Italic = font.Italic,
                RangeStart = (ushort)Math.Clamp(firstChar, ushort.MinValue, ushort.MaxValue),
                Charset = (byte)Math.Clamp(font.CharSet, byte.MinValue, byte.MaxValue),
                AntiAliasing = (byte)Math.Clamp(font.AntiAlias, byte.MinValue, byte.MaxValue),
                RangeEnd = (uint)Math.Max(0, lastChar),
                Texture = texturePageItem,
                ScaleX = 1f,
                ScaleY = 1f,
            };

            foreach (var glyph in font.Glyphs.OrderBy(static glyph => glyph.Character))
            {
                var fontGlyph = new UndertaleFont.Glyph
                {
                    Character = (ushort)Math.Clamp(glyph.Character, ushort.MinValue, ushort.MaxValue),
                    SourceX = (ushort)Math.Clamp(glyph.X, ushort.MinValue, ushort.MaxValue),
                    SourceY = (ushort)Math.Clamp(glyph.Y, ushort.MinValue, ushort.MaxValue),
                    SourceWidth = (ushort)Math.Clamp(glyph.Width, ushort.MinValue, ushort.MaxValue),
                    SourceHeight = (ushort)Math.Clamp(glyph.Height, ushort.MinValue, ushort.MaxValue),
                    Shift = (short)Math.Clamp(glyph.Shift, short.MinValue, short.MaxValue),
                    Offset = (short)Math.Clamp(glyph.Offset, short.MinValue, short.MaxValue),
                };

                foreach (var kerning in glyph.Kerning)
                {
                    fontGlyph.Kerning.Add(new UndertaleFont.Glyph.GlyphKerning
                    {
                        Character = (short)Math.Clamp(kerning.Other, short.MinValue, short.MaxValue),
                        ShiftModifier = (short)Math.Clamp(kerning.Amount, short.MinValue, short.MaxValue),
                    });
                }

                fontEntry.Glyphs.Add(fontGlyph);
            }

            data.Fonts.Add(fontEntry);
        }
    }

    private static Dictionary<GM.GameObject, UndertaleGameObject> CreateObjectShells(UndertaleData data, IEnumerable<GM.GameObject> objects)
    {
        var map = new Dictionary<GM.GameObject, UndertaleGameObject>();

        foreach (var gameObject in objects)
        {
            var objectEntry = new UndertaleGameObject
            {
                Name = data.Strings.MakeString(gameObject.Name),
            };

            data.GameObjects.Add(objectEntry);
            map[gameObject] = objectEntry;
        }

        return map;
    }

    private static void PopulateObjects(
        IEnumerable<GM.GameObject> objects,
        IReadOnlyDictionary<GM.GameObject, UndertaleGameObject> objectMap,
        IReadOnlyDictionary<GM.Sprite, UndertaleSprite> spriteMap)
    {
        foreach (var gameObject in objects)
        {
            var objectEntry = objectMap[gameObject];
            objectEntry.Sprite = MapResource(spriteMap, gameObject.Sprite);
            objectEntry.Visible = gameObject.Visible;
            objectEntry.Solid = gameObject.Solid;
            objectEntry.Depth = gameObject.Depth;
            objectEntry.Persistent = gameObject.Persistent;
            objectEntry.ParentId = MapResource(objectMap, gameObject.Parent);
            objectEntry.TextureMaskId = MapResource(spriteMap, gameObject.Mask);
            objectEntry.UsesPhysics = gameObject.PhysicsObject;
            objectEntry.IsSensor = gameObject.PhysicsObjectSensor;
            objectEntry.CollisionShape = (CollisionShapeFlags)Math.Clamp(gameObject.PhysicsObjectShape, 0, 2);
            objectEntry.Density = gameObject.PhysicsObjectDensity;
            objectEntry.Restitution = gameObject.PhysicsObjectRestitution;
            objectEntry.Group = (uint)Math.Max(0, gameObject.PhysicsObjectGroup);
            objectEntry.LinearDamping = gameObject.PhysicsObjectLinearDamping;
            objectEntry.AngularDamping = gameObject.PhysicsObjectAngularDamping;
            objectEntry.Friction = gameObject.PhysicsObjectFriction;
            objectEntry.Awake = gameObject.PhysicsObjectAwake;
            objectEntry.Kinematic = gameObject.PhysicsObjectKinematic;

            foreach (var point in gameObject.PhysicsShapePoints)
            {
                objectEntry.PhysicsVertices.Add(new UndertaleGameObject.UndertalePhysicsVertex
                {
                    X = point.X,
                    Y = point.Y,
                });
            }
        }
    }

    private static List<(UndertaleCode Code, string Source)> CreateExtensions(UndertaleData data, IEnumerable<GM.Extension> extensions)
    {
        var extensionScripts = new List<(UndertaleCode Code, string Source)>();
        var nextFunctionId = 1u;

        foreach (var extension in extensions)
        {
            var extensionEntry = new UndertaleExtension
            {
                FolderName = data.Strings.MakeString(extension.InstallDir ?? string.Empty),
                Name = data.Strings.MakeString(extension.Name),
                ClassName = data.Strings.MakeString(extension.ClassName ?? string.Empty),
                Version = data.Strings.MakeString(string.IsNullOrWhiteSpace(extension.Version) ? "1.0.0" : extension.Version),
            };

            foreach (var include in extension.Includes)
            {
                var fileName = !string.IsNullOrWhiteSpace(include.FileName)
                    ? include.FileName
                    : include.OriginalName;
                if (string.IsNullOrWhiteSpace(fileName))
                {
                    continue;
                }

                var fileKind = MapExtensionKind(include.Kind);
                var sourceText = DecodeExtensionText(include.RawData);
                if (fileKind == UndertaleExtensionKind.GML
                    && string.IsNullOrWhiteSpace(sourceText)
                    && include.Functions.Count == 0)
                {
                    continue;
                }

                var fileEntry = new UndertaleExtensionFile
                {
                    Filename = data.Strings.MakeString(fileName),
                    InitScript = data.Strings.MakeString(include.Init ?? string.Empty),
                    CleanupScript = data.Strings.MakeString(include.Final ?? string.Empty),
                    Kind = fileKind,
                };

                if (fileKind == UndertaleExtensionKind.GML)
                {
                    foreach (var scriptSection in ExtractExtensionScriptSections(sourceText, include))
                    {
                        if (data.Scripts.ByName(scriptSection.Name) is not null)
                        {
                            continue;
                        }

                        var code = UndertaleCode.CreateEmptyEntry(data, GetScriptCodeName(scriptSection.Name));
                        data.Scripts.Add(new UndertaleScript
                        {
                            Name = data.Strings.MakeString(scriptSection.Name),
                            Code = code,
                        });
                        extensionScripts.Add((code, scriptSection.Source));
                    }
                }
                else
                {
                    foreach (var function in include.Functions)
                    {
                        if (string.IsNullOrWhiteSpace(function.Name))
                        {
                            continue;
                        }

                        fileEntry.Functions.DefineExtensionFunction(
                            data.Functions,
                            data.Strings,
                            nextFunctionId++,
                            (uint)Math.Max(0, function.Kind),
                            function.Name,
                            MapExtensionVarType(function.ReturnType),
                            string.IsNullOrWhiteSpace(function.ExternalName) ? function.Name : function.ExternalName,
                            function.Args.Select(MapExtensionVarType).ToArray());
                    }
                }

                extensionEntry.Files.Add(fileEntry);
            }

            data.Extensions.Add(extensionEntry);
            data.FORM.EXTN.productIdData.Add(ParseProductIdData(extension));
        }

        return extensionScripts;
    }

    private static void CreateTimelines(UndertaleData data, IEnumerable<GM.Timeline> timelines)
    {
        foreach (var timeline in timelines)
        {
            var timelineEntry = new UndertaleTimeline
            {
                Name = data.Strings.MakeString(timeline.Name),
            };

            var orderedMoments = timeline.Moments
                .OrderBy(static moment => moment.Step)
                .ToList();

            for (var index = 0; index < orderedMoments.Count; index++)
            {
                var moment = orderedMoments[index];
                var code = UndertaleCode.CreateEmptyEntry(data, GetTimelineCodeName(timeline.Name, index));
                var eventActions = new UndertalePointerList<UndertaleGameObject.EventAction>
                {
                    CreateCodeAction(code),
                };

                timelineEntry.Moments.Add(new UndertaleTimeline.UndertaleTimelineMoment((uint)Math.Max(0, moment.Step), eventActions));
            }

            data.Timelines.Add(timelineEntry);
        }
    }

    private static void CreateRooms(
        UndertaleData data,
        IEnumerable<GM.Room> rooms,
        IReadOnlyDictionary<GM.Background, UndertaleBackground> backgroundMap,
        IReadOnlyDictionary<GM.GameObject, UndertaleGameObject> objectMap)
    {
        foreach (var room in rooms)
        {
            var roomEntry = new UndertaleRoom
            {
                Name = data.Strings.MakeString(room.Name),
                Caption = data.Strings.MakeString(room.Caption ?? string.Empty),
                Width = (uint)Math.Max(1, room.Width),
                Height = (uint)Math.Max(1, room.Height),
                Speed = (uint)Math.Max(1, room.Speed),
                Persistent = room.Persistent,
                BackgroundColor = BuildArgbColor(room.Colour),
                DrawBackgroundColor = room.ShowColour,
                Flags = BuildRoomFlags(room),
                World = room.PhysicsWorld,
                Top = (uint)Math.Max(0, room.PhysicsWorldTop),
                Left = (uint)Math.Max(0, room.PhysicsWorldLeft),
                Right = (uint)Math.Max(0, room.PhysicsWorldRight),
                Bottom = (uint)Math.Max(0, room.PhysicsWorldBottom),
                GravityX = room.PhysicsWorldGravityX,
                GravityY = room.PhysicsWorldGravityY,
                MetersPerPixel = room.PhysicsWorldPixToMeters,
            };

            if (!string.IsNullOrWhiteSpace(room.Code))
            {
                roomEntry.CreationCodeId = UndertaleCode.CreateEmptyEntry(data, GetRoomCodeName(room.Name));
            }

            for (var index = 0; index < Math.Min(room.Backgrounds.Count, roomEntry.Backgrounds.Count); index++)
            {
                var sourceBackground = room.Backgrounds[index];
                var targetBackground = roomEntry.Backgrounds[index];
                targetBackground.ParentRoom = roomEntry;
                targetBackground.Enabled = sourceBackground.Visible;
                targetBackground.Foreground = sourceBackground.Foreground;
                targetBackground.BackgroundDefinition = MapResource(backgroundMap, sourceBackground.Background);
                targetBackground.X = sourceBackground.X;
                targetBackground.Y = sourceBackground.Y;
                targetBackground.TiledHorizontally = sourceBackground.HTiled;
                targetBackground.TiledVertically = sourceBackground.VTiled;
                targetBackground.SpeedX = sourceBackground.HSpeed;
                targetBackground.SpeedY = sourceBackground.VSpeed;
                targetBackground.Stretch = sourceBackground.Stretch;
            }

            for (var index = 0; index < Math.Min(room.Views.Count, roomEntry.Views.Count); index++)
            {
                var sourceView = room.Views[index];
                var targetView = roomEntry.Views[index];
                targetView.Enabled = sourceView.Visible;
                targetView.ViewX = sourceView.XView;
                targetView.ViewY = sourceView.YView;
                targetView.ViewWidth = sourceView.WView;
                targetView.ViewHeight = sourceView.HView;
                targetView.PortX = sourceView.XPort;
                targetView.PortY = sourceView.YPort;
                targetView.PortWidth = sourceView.WPort;
                targetView.PortHeight = sourceView.HPort;
                targetView.BorderX = (uint)Math.Max(0, sourceView.HBorder);
                targetView.BorderY = (uint)Math.Max(0, sourceView.VBorder);
                targetView.SpeedX = sourceView.HSpeed;
                targetView.SpeedY = sourceView.VSpeed;
                targetView.ObjectId = MapResource(objectMap, sourceView.FollowObject);
            }

            foreach (var instance in room.Instances)
            {
                var roomObject = new UndertaleRoom.GameObject
                {
                    X = instance.X,
                    Y = instance.Y,
                    ObjectDefinition = MapResource(objectMap, instance.Object),
                    InstanceID = (uint)Math.Max(0, instance.Id),
                    ScaleX = (float)instance.ScaleX,
                    ScaleY = (float)instance.ScaleY,
                    Color = instance.Colour,
                    Rotation = (float)instance.Rotation,
                };

                if (!string.IsNullOrWhiteSpace(instance.Code))
                {
                    roomObject.CreationCode = UndertaleCode.CreateEmptyEntry(data, GetRoomInstanceCodeName(room.Name, instance.Id));
                }

                roomEntry.GameObjects.Add(roomObject);
            }

            foreach (var tile in room.Tiles)
            {
                var roomTile = new UndertaleRoom.Tile
                {
                    X = tile.X,
                    Y = tile.Y,
                    BackgroundDefinition = MapResource(backgroundMap, tile.Background),
                    SourceX = tile.SourceX,
                    SourceY = tile.SourceY,
                    Width = (uint)Math.Max(0, tile.Width),
                    Height = (uint)Math.Max(0, tile.Height),
                    TileDepth = tile.Depth,
                    InstanceID = (uint)Math.Max(0, tile.Id),
                    ScaleX = (float)tile.ScaleX,
                    ScaleY = (float)tile.ScaleY,
                    Color = BuildArgbColor(tile.Blend, tile.Alpha),
                };

                roomEntry.Tiles.Add(roomTile);
            }

            roomEntry.SetupRoom(calculateGridWidth: false, calculateGridHeight: false);
            data.Rooms.Add(roomEntry);
            data.GeneralInfo.RoomOrder.Add(new UndertaleResourceById<UndertaleRoom, UndertaleChunkROOM>
            {
                Resource = roomEntry,
            });
        }
    }

    private static void QueueScriptCompilation(
        CodeImportGroup importer,
        IEnumerable<GM.Script> scripts,
        IReadOnlyDictionary<GM.Script, UndertaleCode> scriptCodeMap)
    {
        foreach (var script in scripts)
        {
            importer.QueueReplace(scriptCodeMap[script], NormalizeCode(script.SourceCode));
        }
    }

    private static void QueueExtensionScriptCompilation(CodeImportGroup importer, IEnumerable<(UndertaleCode Code, string Source)> extensionScripts)
    {
        foreach (var extensionScript in extensionScripts)
        {
            importer.QueueReplace(extensionScript.Code, NormalizeCode(extensionScript.Source));
        }
    }

    private static void QueueObjectCompilation(
        UndertaleData data,
        CodeImportGroup importer,
        IEnumerable<GM.GameObject> objects,
        IReadOnlyDictionary<GM.GameObject, UndertaleGameObject> objectMap)
    {
        foreach (var gameObject in objects)
        {
            var objectEntry = objectMap[gameObject];
            foreach (var gameObjectEvent in gameObject.Events)
            {
                var eventType = MapEventType(gameObjectEvent.EventType);
                var eventSubtype = GetEventSubtype(data, gameObjectEvent, objectMap);
                var code = objectEntry.EventHandlerFor(eventType, eventSubtype, data);
                importer.QueueReplace(code, BuildEventCode(gameObjectEvent.Actions));
            }
        }
    }

    private static void QueueTimelineCompilation(CodeImportGroup importer, IList<GM.Timeline> timelines, IList<UndertaleTimeline> timelineEntries)
    {
        for (var timelineIndex = 0; timelineIndex < Math.Min(timelines.Count, timelineEntries.Count); timelineIndex++)
        {
            var sourceTimeline = timelines[timelineIndex];
            var targetTimeline = timelineEntries[timelineIndex];
            var orderedMoments = sourceTimeline.Moments
                .OrderBy(static moment => moment.Step)
                .ToList();

            for (var momentIndex = 0; momentIndex < Math.Min(orderedMoments.Count, targetTimeline.Moments.Count); momentIndex++)
            {
                var targetMoment = targetTimeline.Moments[momentIndex];
                var code = targetMoment.Event.FirstOrDefault()?.CodeId;
                if (code is null)
                {
                    continue;
                }

                importer.QueueReplace(code, BuildEventCode(orderedMoments[momentIndex].Actions));
            }
        }
    }

    private static void QueueRoomCompilation(CodeImportGroup importer, IList<GM.Room> rooms, IList<UndertaleRoom> roomEntries)
    {
        for (var roomIndex = 0; roomIndex < Math.Min(rooms.Count, roomEntries.Count); roomIndex++)
        {
            var sourceRoom = rooms[roomIndex];
            var targetRoom = roomEntries[roomIndex];

            if (targetRoom.CreationCodeId is not null)
            {
                importer.QueueReplace(targetRoom.CreationCodeId, NormalizeCode(sourceRoom.Code));
            }

            for (var instanceIndex = 0; instanceIndex < Math.Min(sourceRoom.Instances.Count, targetRoom.GameObjects.Count); instanceIndex++)
            {
                var code = targetRoom.GameObjects[instanceIndex].CreationCode;
                if (code is null)
                {
                    continue;
                }

                importer.QueueReplace(code, NormalizeCode(sourceRoom.Instances[instanceIndex].Code));
            }
        }
    }

    private static void UpdateGeneralInfoCounters(UndertaleData data)
    {
        data.GeneralInfo.LastObj = Math.Max(
            100000u,
            data.Rooms.SelectMany(static room => room.GameObjects).DefaultIfEmpty().Max(static instance => instance?.InstanceID ?? 0u));
        data.GeneralInfo.LastTile = Math.Max(
            10000000u,
            data.Rooms.SelectMany(static room => room.Tiles).DefaultIfEmpty().Max(static tile => tile?.InstanceID ?? 0u));
    }

    private static UndertaleTexturePageItem? CreateTexturePageItem(
        UndertaleData data,
        string name,
        Bitmap? bitmap,
        int fallbackWidth = 0,
        int fallbackHeight = 0)
    {
        var width = bitmap?.PixelSize.Width ?? Math.Max(0, fallbackWidth);
        var height = bitmap?.PixelSize.Height ?? Math.Max(0, fallbackHeight);
        if (width <= 0 || height <= 0)
        {
            return null;
        }

        GMImage image;
        if (bitmap is not null)
        {
            using var stream = new MemoryStream();
            bitmap.Save(stream);
            image = GMImage.FromPng(stream.ToArray());
        }
        else
        {
            image = new GMImage(width, height).ConvertToFormat(GMImage.ImageFormat.Png);
        }

        var embeddedTexture = new UndertaleEmbeddedTexture
        {
            Name = data.Strings.MakeString(name),
            TextureData = new UndertaleEmbeddedTexture.TexData
            {
                Image = image,
            },
            TextureLoaded = true,
            TextureExternal = false,
        };
        data.EmbeddedTextures.Add(embeddedTexture);

        var texturePageItem = new UndertaleTexturePageItem
        {
            Name = data.Strings.MakeString(name),
            SourceX = 0,
            SourceY = 0,
            SourceWidth = ToUShort(width),
            SourceHeight = ToUShort(height),
            TargetX = 0,
            TargetY = 0,
            TargetWidth = ToUShort(width),
            TargetHeight = ToUShort(height),
            BoundingWidth = ToUShort(width),
            BoundingHeight = ToUShort(height),
            TexturePage = embeddedTexture,
        };
        data.TexturePageItems.Add(texturePageItem);

        return texturePageItem;
    }

    private static (int Width, int Height) GetSpriteSize(GM.Sprite sprite)
    {
        var width = sprite.Width;
        var height = sprite.Height;

        foreach (var frame in sprite.Frames)
        {
            width = Math.Max(width, frame.Width);
            height = Math.Max(height, frame.Height);
        }

        return (Math.Max(1, width), Math.Max(1, height));
    }

    private static void ApplySpriteMargins(UndertaleSprite undertaleSprite, int width, int height, SpriteCollisionBounds bounds)
    {
        if (width <= 0 || height <= 0)
        {
            undertaleSprite.MarginLeft = 0;
            undertaleSprite.MarginTop = 0;
            undertaleSprite.MarginRight = 0;
            undertaleSprite.MarginBottom = 0;
            return;
        }

        undertaleSprite.MarginLeft = Math.Clamp(bounds.Left, 0, width - 1);
        undertaleSprite.MarginRight = Math.Clamp(bounds.Right, 0, width - 1);
        undertaleSprite.MarginTop = Math.Clamp(bounds.Top, 0, height - 1);
        undertaleSprite.MarginBottom = Math.Clamp(bounds.Bottom, 0, height - 1);
    }

    private static UndertaleSprite.SepMaskType MapCollisionKind(GM.SpriteCollisionKind collisionKind)
    {
        return collisionKind switch
        {
            GM.SpriteCollisionKind.Precise or
            GM.SpriteCollisionKind.PrecisePerFrame or
            GM.SpriteCollisionKind.Ellipse or
            GM.SpriteCollisionKind.Diamond => UndertaleSprite.SepMaskType.Precise,
            GM.SpriteCollisionKind.RotatedRectangle => UndertaleSprite.SepMaskType.RotatedRect,
            _ => UndertaleSprite.SepMaskType.AxisAlignedRect,
        };
    }

    private static GMImage? CreateMaskSourceImage(Bitmap? bitmap)
    {
        if (bitmap is null)
        {
            return null;
        }

        using var stream = new MemoryStream();
        bitmap.Save(stream);
        return GMImage.FromPng(stream.ToArray()).ConvertToFormat(GMImage.ImageFormat.RawBgra);
    }

    private static SpriteCollisionBounds GetSpriteCollisionBounds(
        GM.Sprite sprite,
        int width,
        int height,
        IReadOnlyList<GMImage?> frameImages)
    {
        if (width <= 0 || height <= 0)
        {
            return new SpriteCollisionBounds(0, 0, 0, 0);
        }

        if (sprite.BoundingBoxMode == GM.SpriteBoundingBoxMode.FullImage)
        {
            return new SpriteCollisionBounds(0, 0, width - 1, height - 1);
        }

        if (sprite.BoundingBoxMode == GM.SpriteBoundingBoxMode.Manual)
        {
            return new SpriteCollisionBounds(
                Math.Clamp(sprite.BoundingBoxLeft, 0, width - 1),
                Math.Clamp(sprite.BoundingBoxTop, 0, height - 1),
                Math.Clamp(sprite.BoundingBoxRight, 0, width - 1),
                Math.Clamp(sprite.BoundingBoxBottom, 0, height - 1));
        }

        return new SpriteCollisionBounds(
            Math.Clamp(sprite.BoundingBoxLeft, 0, width - 1),
            Math.Clamp(sprite.BoundingBoxTop, 0, height - 1),
            Math.Clamp(sprite.BoundingBoxRight, 0, width - 1),
            Math.Clamp(sprite.BoundingBoxBottom, 0, height - 1));
    }

    private static void PopulateSpriteMask(
        UndertaleSprite.MaskEntry maskEntry,
        GM.Sprite sprite,
        int width,
        int height,
        SpriteCollisionBounds bounds,
        IReadOnlyList<GMImage?> frameImages,
        int maskIndex,
        int maskCount)
    {
        if (maskEntry.Data.Length == 0 || width <= 0 || height <= 0)
        {
            return;
        }

        var mergedPixels = new bool[width * height];
        var startFrame = maskCount == 1 ? 0 : Math.Clamp(maskIndex, 0, frameImages.Count - 1);
        var endFrame = maskCount == 1 ? frameImages.Count : Math.Min(frameImages.Count, startFrame + 1);

        for (var frameIndex = startFrame; frameIndex < endFrame; frameIndex++)
        {
            MergeFrameMask(mergedPixels, sprite, frameImages[frameIndex], width, height, bounds);
        }

        WriteMaskBits(maskEntry.Data, mergedPixels, width, height);
    }

    private static void MergeFrameMask(
        bool[] mergedPixels,
        GM.Sprite sprite,
        GMImage? image,
        int width,
        int height,
        SpriteCollisionBounds bounds)
    {
        switch (sprite.CollisionKind)
        {
            case GM.SpriteCollisionKind.Precise:
            case GM.SpriteCollisionKind.PrecisePerFrame:
                MergePreciseMask(mergedPixels, image, width, height, bounds, (int)Math.Clamp(sprite.CollisionTolerance, 0u, 255u));
                break;

            case GM.SpriteCollisionKind.Ellipse:
                FillEllipseMask(mergedPixels, width, height, bounds);
                break;

            case GM.SpriteCollisionKind.Diamond:
                FillDiamondMask(mergedPixels, width, height, bounds);
                break;

            case GM.SpriteCollisionKind.Rectangle:
            case GM.SpriteCollisionKind.RotatedRectangle:
            case GM.SpriteCollisionKind.SpineMesh:
            default:
                FillRectangleMask(mergedPixels, width, height, bounds);
                break;
        }
    }

    private static void MergePreciseMask(
        bool[] mergedPixels,
        GMImage? image,
        int width,
        int height,
        SpriteCollisionBounds bounds,
        int tolerance)
    {
        if (image is null)
        {
            return;
        }

        var raw = image.GetRawImageData();
        var minX = Math.Clamp(bounds.Left, 0, Math.Min(width, image.Width) - 1);
        var maxX = Math.Clamp(bounds.Right, 0, Math.Min(width, image.Width) - 1);
        var minY = Math.Clamp(bounds.Top, 0, Math.Min(height, image.Height) - 1);
        var maxY = Math.Clamp(bounds.Bottom, 0, Math.Min(height, image.Height) - 1);
        for (var y = minY; y <= maxY; y++)
        {
            for (var x = minX; x <= maxX; x++)
            {
                var pixelOffset = ((y * image.Width) + x) * 4;
                if (raw[pixelOffset + 3] > tolerance)
                {
                    mergedPixels[(y * width) + x] = true;
                }
            }
        }
    }

    private static void FillRectangleMask(bool[] mergedPixels, int width, int height, SpriteCollisionBounds bounds)
    {
        for (var y = bounds.Top; y <= bounds.Bottom && y < height; y++)
        {
            for (var x = bounds.Left; x <= bounds.Right && x < width; x++)
            {
                mergedPixels[(y * width) + x] = true;
            }
        }
    }

    private static void FillEllipseMask(bool[] mergedPixels, int width, int height, SpriteCollisionBounds bounds)
    {
        var left = bounds.Left;
        var top = bounds.Top;
        var rightExclusive = bounds.Right + 1;
        var bottomExclusive = bounds.Bottom + 1;
        var centerX = (float)((left + (rightExclusive - 1)) / 2);
        var centerY = (float)((bottomExclusive - 1 + top) / 2);
        var radiusX = centerX - left + 0.5f;
        var radiusY = centerY - top + 0.5f;

        for (var y = top; y < bottomExclusive && y < height; y++)
        {
            for (var x = left; x < rightExclusive && x < width; x++)
            {
                if (radiusX > 0f &&
                    radiusY > 0f &&
                    Math.Pow((x - centerX) / radiusX, 2.0) + Math.Pow((y - centerY) / radiusY, 2.0) <= 1.0)
                {
                    mergedPixels[(y * width) + x] = true;
                }
            }
        }
    }

    private static void FillDiamondMask(bool[] mergedPixels, int width, int height, SpriteCollisionBounds bounds)
    {
        var left = bounds.Left;
        var top = bounds.Top;
        var rightExclusive = bounds.Right + 1;
        var bottomExclusive = bounds.Bottom + 1;
        var centerX = (float)((left + (rightExclusive - 1)) / 2);
        var centerY = (float)((bottomExclusive - 1 + top) / 2);
        var radiusX = centerX - left + 0.5f;
        var radiusY = centerY - top + 0.5f;

        for (var y = top; y < bottomExclusive && y < height; y++)
        {
            for (var x = left; x < rightExclusive && x < width; x++)
            {
                if (radiusX > 0f &&
                    radiusY > 0f &&
                    Math.Abs((x - centerX) / radiusX) + Math.Abs((y - centerY) / radiusY) <= 1f)
                {
                    mergedPixels[(y * width) + x] = true;
                }
            }
        }
    }

    private static void WriteMaskBits(byte[] output, bool[] pixels, int width, int height)
    {
        Array.Clear(output, 0, output.Length);
        var rowStride = (width + 7) / 8;
        for (var y = 0; y < height; y++)
        {
            var rowOffset = y * rowStride;
            for (var x = 0; x < width; x++)
            {
                if (!pixels[(y * width) + x])
                {
                    continue;
                }

                output[rowOffset + (x / 8)] |= (byte)(1 << (7 - (x % 8)));
            }
        }
    }

    private readonly record struct SpriteCollisionBounds(int Left, int Top, int Right, int Bottom);

    private static string NormalizeSoundExtension(string? extension, string? originalName)
    {
        var candidate = !string.IsNullOrWhiteSpace(extension)
            ? extension
            : Path.GetExtension(originalName ?? string.Empty);
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return ".wav";
        }

        return candidate.StartsWith(".", StringComparison.Ordinal)
            ? candidate.ToLowerInvariant()
            : "." + candidate.ToLowerInvariant();
    }

    private static string GetSoundFileName(GM.Sound sound, string extension)
    {
        var originalName = Path.GetFileName(sound.OriginalName ?? string.Empty);
        if (!string.IsNullOrWhiteSpace(originalName))
        {
            return Path.HasExtension(originalName)
                ? originalName
                : originalName + extension;
        }

        return sound.Name + extension;
    }

    private static string GetExternalSoundFileName(GM.Sound sound)
    {
        var originalName = Path.GetFileName(sound.OriginalName ?? string.Empty);
        if (!string.IsNullOrWhiteSpace(originalName))
        {
            return originalName;
        }

        return sound.Name + NormalizeSoundExtension(sound.Extension, sound.OriginalName);
    }

    private static UndertaleSound.AudioEntryFlags BuildSoundFlags(GM.Sound sound)
    {
        if (sound.Streamed)
        {
            return UndertaleSound.AudioEntryFlags.Regular;
        }

        var flags = UndertaleSound.AudioEntryFlags.Regular | UndertaleSound.AudioEntryFlags.IsEmbedded;

        if (sound.UncompressOnLoad)
        {
            flags |= UndertaleSound.AudioEntryFlags.IsDecompressedOnLoad;
        }
        else if (sound.Compressed)
        {
            flags |= UndertaleSound.AudioEntryFlags.IsCompressed;
        }

        return flags;
    }

    private static UndertaleShader.ShaderType MapShaderType(string? projectType)
    {
        return (projectType ?? string.Empty).ToUpperInvariant() switch
        {
            "GLSL" => UndertaleShader.ShaderType.GLSL,
            "HLSL9" => UndertaleShader.ShaderType.HLSL9,
            "HLSL11" => UndertaleShader.ShaderType.HLSL11,
            "PSSL" => UndertaleShader.ShaderType.PSSL,
            "CG_PSVITA" => UndertaleShader.ShaderType.Cg_PSVita,
            "CG_PS3" => UndertaleShader.ShaderType.Cg_PS3,
            _ => UndertaleShader.ShaderType.GLSL_ES,
        };
    }

    private static ShaderTexts BuildShaderTexts(
        UndertaleShader.ShaderType shaderType,
        string shaderName,
        string preparedVertexSource,
        string preparedFragmentSource)
    {
        return shaderType switch
        {
            UndertaleShader.ShaderType.GLSL_ES => BuildGlslEsShaderTexts(shaderName, preparedVertexSource, preparedFragmentSource),
            UndertaleShader.ShaderType.GLSL => new ShaderTexts(
                string.Empty,
                string.Empty,
                GlslVertexPrefix + LoadShaderCompilerTextFile("VShaderCommon.shader") + ShaderDefineGlsl + RestoreCombinedShaderVertexSpacing(preparedVertexSource),
                GlslFragmentPrefix + LoadShaderCompilerTextFile("FShaderCommon.shader") + ShaderDefineGlsl + preparedFragmentSource,
                string.Empty,
                string.Empty),
            UndertaleShader.ShaderType.HLSL9 => new ShaderTexts(
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                LoadShaderCompilerTextFile("HLSL9_VShaderCommon.shader") + preparedVertexSource,
                LoadShaderCompilerTextFile("HLSL9_PShaderCommon.shader") + preparedFragmentSource),
            _ => new ShaderTexts(string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty),
        };
    }

    private static void PopulateCompiledShaderData(
        UndertaleShader shaderEntry,
        UndertaleShader.ShaderType shaderType,
        string shaderName,
        string preparedVertexSource,
        string preparedFragmentSource)
    {
        switch (shaderType)
        {
            case UndertaleShader.ShaderType.HLSL11:
            {
                var (vertexData, pixelData) = CompileHlsl11(shaderName, preparedVertexSource, preparedFragmentSource);
                AssignRawShaderData(shaderEntry.HLSL11_VertexData, vertexData);
                AssignRawShaderData(shaderEntry.HLSL11_PixelData, pixelData);
                break;
            }
        }
    }

    private static void AssignRawShaderData(UndertaleShader.UndertaleRawShaderData target, byte[] data)
    {
        target.Data = data;
        target.IsNull = data.Length == 0;
    }

    private static ShaderTexts BuildGlslEsShaderTexts(
        string shaderName,
        string preparedVertexSource,
        string preparedFragmentSource)
    {
        var (compiledHlsl9Vertex, compiledHlsl9Fragment) = CompileHlsl9FromGlslEs(shaderName, preparedVertexSource, preparedFragmentSource);
        return new ShaderTexts(
            GlslEsVertexPrefix + LoadShaderCompilerTextFile("VShaderCommon.shader") + ShaderDefineGlslEs + RestoreCombinedShaderVertexSpacing(preparedVertexSource),
            GlslEsFragmentPrefix + LoadShaderCompilerTextFile("FShaderCommon.shader") + ShaderDefineGlslEs + preparedFragmentSource,
            GlslVertexPrefix + LoadShaderCompilerTextFile("VShaderCommon.shader") + ShaderDefineGlsl + RestoreCombinedShaderVertexSpacing(preparedVertexSource),
            GlslFragmentPrefix + LoadShaderCompilerTextFile("FShaderCommon.shader") + ShaderDefineGlsl + preparedFragmentSource,
            LoadShaderCompilerTextFile("HLSL9_VShaderCommon.shader") + compiledHlsl9Vertex,
            LoadShaderCompilerTextFile("HLSL9_PShaderCommon.shader") + compiledHlsl9Fragment);
    }

    private static (string VertexShader, string PixelShader) CompileHlsl9FromGlslEs(
        string shaderName,
        string preparedVertexSource,
        string preparedFragmentSource)
    {
        var compilerDirectory = ResolveShaderCompilerDirectory();
        var tempDirectory = CreateShaderTempDirectory(shaderName);

        try
        {
            var shaderFilePath = WriteCombinedShaderFile(tempDirectory, shaderName, preparedVertexSource, preparedFragmentSource);
            var outputVertexShaderPath = Path.Combine(tempDirectory, "vout.shader");
            var outputFragmentShaderPath = Path.Combine(tempDirectory, "fout.shader");

            RunShaderCompilerProcess(
                Path.Combine(compilerDirectory, "HLSLCompiler.exe"),
                new[]
                {
                    "-shader", shaderFilePath,
                    "-name", BuildShaderCompilerEntryName(shaderName),
                    "-out", tempDirectory,
                    "-preamble", compilerDirectory,
                    "-typedefine", ShaderDefineHlsl9.TrimEnd('\r', '\n'),
                },
                tempDirectory,
                shaderName,
                "GLSLES to HLSL9 translation");

            return (File.ReadAllText(outputVertexShaderPath), File.ReadAllText(outputFragmentShaderPath));
        }
        finally
        {
            TryDeleteDirectory(tempDirectory);
        }
    }

    private static (byte[] VertexData, byte[] PixelData) CompileHlsl11(
        string shaderName,
        string preparedVertexSource,
        string preparedFragmentSource)
    {
        var compilerDirectory = ResolveShaderCompilerDirectory();
        var tempDirectory = CreateShaderTempDirectory(shaderName);

        try
        {
            var shaderFilePath = WriteCombinedShaderFile(tempDirectory, shaderName, preparedVertexSource, preparedFragmentSource);
            var vertexDataPath = Path.Combine(tempDirectory, "vout.shdata");
            var pixelDataPath = Path.Combine(tempDirectory, "fout.shdata");

            RunShaderCompilerProcess(
                Path.Combine(compilerDirectory, "D3D11ShaderParser.exe"),
                new[]
                {
                    "-quiet",
                    "-combinedshader",
                    "-profilev", "vs_auto",
                    "-profilep", "ps_auto",
                    "-preamble", compilerDirectory,
                    "-shader", shaderFilePath,
                    "-outv", vertexDataPath,
                    "-outp", pixelDataPath,
                    "-name", BuildShaderCompilerEntryName(shaderName),
                },
                tempDirectory,
                shaderName,
                "HLSL11 compilation");

            return (File.ReadAllBytes(vertexDataPath), File.ReadAllBytes(pixelDataPath));
        }
        finally
        {
            TryDeleteDirectory(tempDirectory);
        }
    }

    private static string ResolveShaderCompilerDirectory()
    {
        foreach (var candidate in EnumerateShaderCompilerDirectories())
        {
            if (Directory.Exists(candidate) &&
                File.Exists(Path.Combine(candidate, "HLSLCompiler.exe")) &&
                File.Exists(Path.Combine(candidate, "D3D11ShaderParser.exe")))
            {
                return candidate;
            }
        }

        throw new InvalidOperationException("Shader compiler tools were not found. Expected a ShaderCompiler folder beside the application output.");
    }

    private static IEnumerable<string> EnumerateShaderCompilerDirectories()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        static IEnumerable<string> EnumerateCandidatesFromBase(string? baseDirectory)
        {
            if (string.IsNullOrWhiteSpace(baseDirectory))
            {
                yield break;
            }

            yield return Path.Combine(baseDirectory, "ShaderCompiler");

            var current = new DirectoryInfo(baseDirectory);
            for (var depth = 0; depth < 8 && current is not null; depth++, current = current.Parent)
            {
                yield return Path.Combine(current.FullName, "ShaderCompiler");
                yield return Path.Combine(current.FullName, "AvaloniaGM", "ShaderCompiler");
            }
        }

        foreach (var candidate in EnumerateCandidatesFromBase(AppContext.BaseDirectory))
        {
            if (seen.Add(candidate))
            {
                yield return candidate;
            }
        }

        var assemblyDirectory = Path.GetDirectoryName(typeof(DataWinSerializer).Assembly.Location);
        foreach (var candidate in EnumerateCandidatesFromBase(assemblyDirectory))
        {
            if (seen.Add(candidate))
            {
                yield return candidate;
            }
        }
    }

    private static string LoadShaderCompilerTextFile(string fileName)
    {
        var path = Path.Combine(ResolveShaderCompilerDirectory(), fileName);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Required shader compiler file was not found: {fileName}", path);
        }

        return File.ReadAllText(path);
    }

    private static string RestoreCombinedShaderVertexSpacing(string vertexSource)
    {
        if (string.IsNullOrEmpty(vertexSource))
        {
            return string.Empty;
        }

        if (vertexSource.EndsWith("\r\n\r\n", StringComparison.Ordinal) ||
            vertexSource.EndsWith("\n\n", StringComparison.Ordinal))
        {
            return vertexSource;
        }

        if (vertexSource.EndsWith("\r\n", StringComparison.Ordinal))
        {
            return vertexSource + "\r\n";
        }

        if (vertexSource.EndsWith("\n", StringComparison.Ordinal))
        {
            return vertexSource + "\n";
        }

        return vertexSource + "\r\n\r\n";
    }

    private static string CreateShaderTempDirectory(string shaderName)
    {
        var safeName = SanitizeFileName(shaderName);
        var directory = Path.Combine(Path.GetTempPath(), "AvaloniaGM", "ShaderCompile", $"{safeName}_{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static string WriteCombinedShaderFile(
        string tempDirectory,
        string shaderName,
        string preparedVertexSource,
        string preparedFragmentSource)
    {
        var path = Path.Combine(tempDirectory, $"{SanitizeFileName(shaderName)}.shader");
        var combinedSource = string.Concat(
            preparedVertexSource,
            Environment.NewLine,
            GM.Shader.SplitMarker,
            Environment.NewLine,
            preparedFragmentSource,
            Environment.NewLine);
        File.WriteAllText(path, combinedSource, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        return path;
    }

    private static string SanitizeFileName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "shader";
        }

        var invalidCharacters = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            builder.Append(invalidCharacters.Contains(character) ? '_' : character);
        }

        return builder.Length == 0 ? "shader" : builder.ToString();
    }

    private static string BuildShaderCompilerEntryName(string shaderName)
    {
        return "Shader_" + Path.GetFileNameWithoutExtension(SanitizeFileName(shaderName));
    }

    private static void RunShaderCompilerProcess(
        string executablePath,
        IEnumerable<string> arguments,
        string workingDirectory,
        string shaderName,
        string stepName)
    {
        if (!File.Exists(executablePath))
        {
            throw new FileNotFoundException($"Required shader compiler executable was not found for {stepName}.", executablePath);
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            UseShellExecute = false,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            WorkingDirectory = workingDirectory,
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Unable to start shader compiler process for {stepName}.");
        var standardOutput = process.StandardOutput.ReadToEnd();
        var standardError = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode == 0)
        {
            return;
        }

        var message = string.Join(
            Environment.NewLine,
            new[]
            {
                $"Shader compilation failed for '{shaderName}' during {stepName}.",
                $"Executable: {executablePath}",
                $"Exit code: {process.ExitCode}",
                string.IsNullOrWhiteSpace(standardOutput) ? string.Empty : "Output:" + Environment.NewLine + standardOutput.Trim(),
                string.IsNullOrWhiteSpace(standardError) ? string.Empty : "Error:" + Environment.NewLine + standardError.Trim(),
            }.Where(static part => !string.IsNullOrWhiteSpace(part)));
        throw new InvalidOperationException(message);
    }

    private static void TryDeleteDirectory(string path)
    {
        if (!Directory.Exists(path))
        {
            return;
        }

        try
        {
            Directory.Delete(path, recursive: true);
        }
        catch
        {
        }
    }

    private static IEnumerable<string> EnumerateShaderAttributes(string? vertexSource)
    {
        if (string.IsNullOrWhiteSpace(vertexSource))
        {
            yield break;
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (Match match in Regex.Matches(vertexSource, @"^\s*attribute\s+\w+\s+([A-Za-z_]\w*)", RegexOptions.Multiline))
        {
            var name = match.Groups[1].Value;
            if (seen.Add(name))
            {
                yield return name;
            }
        }
    }

    private const string GlslEsFragmentPrefix = "precision mediump float;\n#define LOWPREC lowp\n";
    private const string GlslEsVertexPrefix = "#define LOWPREC lowp\n";
    private const string GlslFragmentPrefix = "#version 120\n#define LOWPREC \n";
    private const string GlslVertexPrefix = "#version 120\n#define LOWPREC \n";
    private const string ShaderDefineGlslEs = "#define _YY_GLSLES_ 1\n";
    private const string ShaderDefineGlsl = "#define _YY_GLSL_ 1\n";
    private const string ShaderDefineHlsl9 = "#define _YY_HLSL9_ 1\n";
    private const string ShaderDefineHlsl11 = "#define _YY_HLSL11_ 1\n";

    private readonly record struct ShaderTexts(
        string GlslEsVertex,
        string GlslEsFragment,
        string GlslVertex,
        string GlslFragment,
        string Hlsl9Vertex,
        string Hlsl9Fragment);

    private static string BuildEventCode(IEnumerable<GM.GameObjectAction> actions)
    {
        var codeBlocks = new List<string>();

        foreach (var action in actions)
        {
            if (TryExtractCodeAction(action, out var code))
            {
                codeBlocks.Add(NormalizeCode(code));
                continue;
            }

            var functionName = string.IsNullOrWhiteSpace(action.FunctionName) ? "<unnamed>" : action.FunctionName;
            codeBlocks.Add(string.Create(
                CultureInfo.InvariantCulture,
                $"/* Unsupported DnD action: {functionName}, kind={action.Kind}, exetype={action.ExecuteType} */"));
        }

        return string.Join(Environment.NewLine + Environment.NewLine, codeBlocks);
    }

    private static bool TryExtractCodeAction(GM.GameObjectAction action, out string code)
    {
        if (action.ExecuteType == GM.GameObjectActionExecuteType.Code || action.Kind == GM.GameObjectActionKind.Code)
        {
            code = action.CodeString ?? string.Empty;
            if (string.IsNullOrWhiteSpace(code))
            {
                code = action.Arguments.FirstOrDefault(static argument => !string.IsNullOrWhiteSpace(argument.Value))?.Value ?? string.Empty;
            }
            return true;
        }

        code = string.Empty;
        return false;
    }

    private static uint GetEventSubtype(
        UndertaleData data,
        GM.GameObjectEvent gameObjectEvent,
        IReadOnlyDictionary<GM.GameObject, UndertaleGameObject> objectMap)
    {
        if (gameObjectEvent.EventType == GM.GameObjectEventType.Collision)
        {
            if (gameObjectEvent.CollisionObject is null || !objectMap.TryGetValue(gameObjectEvent.CollisionObject, out var collisionObject))
            {
                return 0;
            }

            var index = data.GameObjects.IndexOf(collisionObject);
            return index < 0 ? 0u : (uint)index;
        }

        return (uint)Math.Max(0, gameObjectEvent.EventNumber);
    }

    private static EventType MapEventType(GM.GameObjectEventType eventType)
    {
        return (EventType)(uint)eventType;
    }

    private static string NormalizeCode(string? code)
    {
        return (code ?? string.Empty)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
    }

    private static string GetScriptCodeName(string name)
    {
        return "gml_Script_" + name;
    }

    private static string GetTimelineCodeName(string timelineName, int index)
    {
        return $"Timeline_{timelineName}_{index}";
    }

    private static string GetRoomCodeName(string roomName)
    {
        return $"gml_Room_{roomName}_Create";
    }

    private static string GetRoomInstanceCodeName(string roomName, int instanceId)
    {
        return $"gml_RoomCC_{roomName}_{Math.Max(0, instanceId)}_Create";
    }

    private static TTarget? MapResource<TSource, TTarget>(
        IReadOnlyDictionary<TSource, TTarget> map,
        TSource? source)
        where TSource : class
        where TTarget : class
    {
        if (source is null)
        {
            return null;
        }

        return map.TryGetValue(source, out var target) ? target : null;
    }

    private static ushort ToUShort(int value)
    {
        return (ushort)Math.Clamp(value, ushort.MinValue, ushort.MaxValue);
    }

    private static uint BuildArgbColor(int rgb, double alpha = 1.0)
    {
        var alphaByte = (uint)Math.Clamp((int)Math.Round(alpha * byte.MaxValue), byte.MinValue, byte.MaxValue);
        return (alphaByte << 24) | ((uint)rgb & 0x00FFFFFFu);
    }

    private static UndertaleRoom.RoomEntryFlags BuildRoomFlags(GM.Room room)
    {
        var flags = (UndertaleRoom.RoomEntryFlags)0;
        if (room.EnableViews)
        {
            flags |= UndertaleRoom.RoomEntryFlags.EnableViews;
        }

        if (room.ViewClearScreen)
        {
            flags |= UndertaleRoom.RoomEntryFlags.ClearViewBackground;
        }

        if (!room.ClearDisplayBuffer)
        {
            flags |= UndertaleRoom.RoomEntryFlags.DoNotClearDisplayBuffer;
        }

        return flags;
    }

    private static UndertaleGameObject.EventAction CreateCodeAction(UndertaleCode code)
    {
        return new UndertaleGameObject.EventAction
        {
            LibID = 1,
            ID = 603,
            Kind = 7,
            UseRelative = false,
            IsQuestion = false,
            UseApplyTo = true,
            ExeType = 2,
            ActionName = code.Name,
            CodeId = code,
            ArgumentCount = 1,
            Who = -1,
            Relative = false,
            IsNot = false,
            UnknownAlwaysZero = 0,
        };
    }

    private static UndertaleExtensionKind MapExtensionKind(int kind)
    {
        return kind switch
        {
            1 => UndertaleExtensionKind.Dll,
            2 => UndertaleExtensionKind.GML,
            5 => UndertaleExtensionKind.Js,
            _ => UndertaleExtensionKind.Generic,
        };
    }

    private static UndertaleExtensionVarType MapExtensionVarType(int type)
    {
        return type == 1 ? UndertaleExtensionVarType.String : UndertaleExtensionVarType.Double;
    }

    private static string DecodeExtensionText(byte[]? rawData)
    {
        if (rawData is null || rawData.Length == 0)
        {
            return string.Empty;
        }

        return NormalizeCode(System.Text.Encoding.UTF8.GetString(rawData)).TrimStart('\uFEFF');
    }

    private static IEnumerable<(string Name, string Source)> ExtractExtensionScriptSections(string sourceText, GM.ExtensionInclude include)
    {
        if (string.IsNullOrWhiteSpace(sourceText))
        {
            yield break;
        }

        var matches = Regex.Matches(sourceText, @"(?m)^[ \t]*#define[ \t]+([A-Za-z_]\w*)[ \t]*$");
        if (matches.Count == 0)
        {
            var fallbackName = include.Functions.FirstOrDefault(static function => !string.IsNullOrWhiteSpace(function.Name))?.Name
                ?? Path.GetFileNameWithoutExtension(include.FileName ?? include.OriginalName ?? string.Empty);
            if (!string.IsNullOrWhiteSpace(fallbackName))
            {
                yield return (fallbackName, sourceText);
            }

            yield break;
        }

        for (var index = 0; index < matches.Count; index++)
        {
            var match = matches[index];
            var name = match.Groups[1].Value;
            var bodyStart = match.Index + match.Length;
            if (bodyStart < sourceText.Length && sourceText[bodyStart] == '\n')
            {
                bodyStart++;
            }

            var bodyEnd = index + 1 < matches.Count ? matches[index + 1].Index : sourceText.Length;
            var body = sourceText[bodyStart..bodyEnd].Trim('\n');
            if (!string.IsNullOrWhiteSpace(name))
            {
                yield return (name, body);
            }
        }
    }

    private static byte[] ParseProductIdData(GM.Extension extension)
    {
        var productId = extension.ProductId;
        if (!string.IsNullOrWhiteSpace(productId))
        {
            if (Guid.TryParse(productId, out var guid))
            {
                return guid.ToByteArray();
            }

            if (productId.Length == 32)
            {
                try
                {
                    var bytes = new byte[16];
                    for (var index = 0; index < bytes.Length; index++)
                    {
                        bytes[index] = Convert.ToByte(productId.Substring(index * 2, 2), 16);
                    }

                    return bytes;
                }
                catch (FormatException)
                {
                }
            }
        }

        return MD5.HashData(System.Text.Encoding.UTF8.GetBytes(extension.Name));
    }
}
