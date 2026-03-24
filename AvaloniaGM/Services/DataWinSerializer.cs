using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
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
        data.GeneralInfo.DefaultWindowWidth = (uint)Math.Max(1, project.Rooms.FirstOrDefault()?.Width ?? 1024);
        data.GeneralInfo.DefaultWindowHeight = (uint)Math.Max(1, project.Rooms.FirstOrDefault()?.Height ?? 768);

        data.Options.Info |= UndertaleOptions.OptionsFlags.CreationEventOrder;
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

            ApplySpriteMargins(undertaleSprite, sprite, size.Width, size.Height);

            foreach (var frame in sprite.Frames.OrderBy(static frame => frame.Index))
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
                undertaleSprite.CollisionMasks.Add(undertaleSprite.NewMaskEntry(data));
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
            var shaderEntry = new UndertaleShader
            {
                Name = data.Strings.MakeString(shader.Name),
                Type = MapShaderType(shader.ProjectType),
                GLSL_ES_Vertex = data.Strings.MakeString(shader.VertexSource ?? string.Empty),
                GLSL_ES_Fragment = data.Strings.MakeString(shader.FragmentSource ?? string.Empty),
                GLSL_Vertex = data.Strings.MakeString(string.Empty),
                GLSL_Fragment = data.Strings.MakeString(string.Empty),
                HLSL9_Vertex = data.Strings.MakeString(string.Empty),
                HLSL9_Fragment = data.Strings.MakeString(string.Empty),
            };

            foreach (var attributeName in EnumerateShaderAttributes(shader.VertexSource))
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

    private static void ApplySpriteMargins(UndertaleSprite undertaleSprite, GM.Sprite sprite, int width, int height)
    {
        if (width <= 0 || height <= 0)
        {
            undertaleSprite.MarginLeft = 0;
            undertaleSprite.MarginTop = 0;
            undertaleSprite.MarginRight = 0;
            undertaleSprite.MarginBottom = 0;
            return;
        }

        if (sprite.BoundingBoxMode == GM.SpriteBoundingBoxMode.Manual)
        {
            undertaleSprite.MarginLeft = Math.Clamp(sprite.BoundingBoxLeft, 0, width - 1);
            undertaleSprite.MarginRight = Math.Clamp(sprite.BoundingBoxRight, 0, width - 1);
            undertaleSprite.MarginTop = Math.Clamp(sprite.BoundingBoxTop, 0, height - 1);
            undertaleSprite.MarginBottom = Math.Clamp(sprite.BoundingBoxBottom, 0, height - 1);
            return;
        }

        undertaleSprite.MarginLeft = 0;
        undertaleSprite.MarginTop = 0;
        undertaleSprite.MarginRight = width - 1;
        undertaleSprite.MarginBottom = height - 1;
    }

    private static UndertaleSprite.SepMaskType MapCollisionKind(GM.SpriteCollisionKind collisionKind)
    {
        return collisionKind switch
        {
            GM.SpriteCollisionKind.Precise or GM.SpriteCollisionKind.PrecisePerFrame => UndertaleSprite.SepMaskType.Precise,
            GM.SpriteCollisionKind.RotatedRectangle => UndertaleSprite.SepMaskType.RotatedRect,
            _ => UndertaleSprite.SepMaskType.AxisAlignedRect,
        };
    }

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
