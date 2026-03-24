using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using AvaloniaGM.Models;
using AvaloniaGM.Services;

namespace AvaloniaGM.ViewModels
{
    public partial class MainWindowViewModel : ViewModelBase
    {
        private static readonly (ProjectResourceKind Kind, string DisplayName)[] DefaultResourceRoots =
        [
            (ProjectResourceKind.Sprite, "Sprites"),
            (ProjectResourceKind.Sound, "Sounds"),
            (ProjectResourceKind.Background, "Backgrounds"),
            (ProjectResourceKind.Path, "Paths"),
            (ProjectResourceKind.Script, "Scripts"),
            (ProjectResourceKind.Shader, "Shaders"),
            (ProjectResourceKind.Font, "Fonts"),
            (ProjectResourceKind.Object, "Objects"),
            (ProjectResourceKind.Timeline, "Timelines"),
            (ProjectResourceKind.Room, "Rooms"),
            (ProjectResourceKind.DataFile, "Datafiles"),
            (ProjectResourceKind.Extension, "Extensions")
        ];

        public ObservableCollection<ResourceTreeItemViewModel> ResourceTree { get; } = new();

        public ObservableCollection<EditorTabViewModel> OpenTabs { get; } = new();

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(WindowTitle))]
        private string projectName = "Untitled Project";

        [ObservableProperty]
        private EditorTabViewModel? selectedTab;

        [ObservableProperty]
        private string outputText = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(WindowTitle))]
        private string? currentProjectFilePath;

        [ObservableProperty]
        private Project? currentProject;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(RunProjectCommand))]
        private bool isRunProjectInProgress;

        public string WindowTitle => $"{ProjectName} - AvaloniaGM";

        public MainWindowViewModel()
        {
            CreateNewProjectShell(writeLog: false);
            AppendOutput("AvaloniaGM shell initialized.");
            AppendOutput("UI layout loaded.");
            AppendOutput("Ready.");
        }

        [RelayCommand]
        private void NewProject()
        {
            CreateNewProjectShell(writeLog: true);
        }

        private bool CanRunProject() => !IsRunProjectInProgress;

        [RelayCommand(CanExecute = nameof(CanRunProject))]
        private async Task RunProject()
        {
            if (string.IsNullOrWhiteSpace(CurrentProjectFilePath))
            {
                AppendOutput("Run Project requires a saved project file.");
                return;
            }

            try
            {
                IsRunProjectInProgress = true;

                var project = EnsureCurrentProject();
                var projectFilePath = Path.GetFullPath(CurrentProjectFilePath);
                var projectDirectory = Path.GetDirectoryName(projectFilePath);
                if (string.IsNullOrWhiteSpace(projectDirectory))
                {
                    AppendOutput("Unable to resolve the project directory.");
                    return;
                }

                var outputDirectory = Path.Combine(projectDirectory, "bin");
                var outputExePath = Path.Combine(outputDirectory, GetProjectNameFromPath(projectFilePath, project.Name) + ".exe");

                AppendOutput($"Building project to: {outputExePath}");
                await Task.Run(() => new ProjectBuilder().Build(project, outputExePath));

                Process.Start(new ProcessStartInfo
                {
                    FileName = outputExePath,
                    WorkingDirectory = outputDirectory,
                    UseShellExecute = true,
                });

                AppendOutput($"Launched project: {outputExePath}");
            }
            catch (Exception ex)
            {
                AppendOutput($"Failed to run project: {ex.Message}");
            }
            finally
            {
                IsRunProjectInProgress = false;
            }
        }

        [RelayCommand]
        private void ShowHelp()
        {
            AppendOutput("Help triggered. About dialog and documentation entry are not implemented yet.");
        }

        public Project EnsureCurrentProject()
        {
            if (CurrentProject is not null)
            {
                return CurrentProject;
            }

            CreateNewProjectShell(writeLog: false);
            return CurrentProject!;
        }

        public void LoadProject(Project project, string projectFilePath)
        {
            ArgumentNullException.ThrowIfNull(project);
            ArgumentException.ThrowIfNullOrWhiteSpace(projectFilePath);

            var fullPath = Path.GetFullPath(projectFilePath);
            project.Name = GetProjectNameFromPath(fullPath, project.Name);
            EnsureProjectResourceRoots(project);
            _selectedTreeItemKey = null;

            CurrentProject = project;
            CurrentProjectFilePath = fullPath;
            ProjectName = project.Name;

            RebuildResourceTree(project, preserveExpandedState: false);
            RebuildOpenTabs(project);

            AppendOutput($"Project opened: {fullPath}");
        }

        public void MarkProjectSaved(string projectFilePath, bool savedAs)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(projectFilePath);

            var fullPath = Path.GetFullPath(projectFilePath);
            var project = EnsureCurrentProject();

            project.Name = GetProjectNameFromPath(fullPath, project.Name);
            EnsureProjectResourceRoots(project);
            CurrentProjectFilePath = fullPath;
            ProjectName = project.Name;

            RebuildResourceTree(project);
            RebuildOpenTabs(project);

            AppendOutput(savedAs
                ? $"Project saved as: {fullPath}"
                : $"Project saved: {fullPath}");
        }

        public void OpenResourceTab(ResourceTreeItemViewModel treeItem)
        {
            ArgumentNullException.ThrowIfNull(treeItem);

            if (treeItem.IsFolder || treeItem.Resource is null)
            {
                return;
            }

            var existingTab = OpenTabs.FirstOrDefault(tab => tab.MatchesResource(treeItem.Kind, treeItem.ResourceKey));
            if (existingTab is not null)
            {
                SelectedTab = existingTab;
                return;
            }

            var replacementTab = SelectedTab is not null && SelectedTab.CanBeReplaced
                ? SelectedTab
                : null;
            var editorContent = BuildEditorContent(treeItem.Kind, treeItem.Resource);

            if (replacementTab is not null)
            {
                replacementTab.ReplaceWith(
                    treeItem.Name,
                    treeItem.Kind,
                    treeItem.ResourceKey,
                    editorContent);
                SelectedTab = replacementTab;
            }
            else
            {
                var resourceTab = EditorTabViewModel.CreateResourceTab(
                    treeItem.Name,
                    treeItem.Kind,
                    treeItem.ResourceKey,
                    editorContent);

                OpenTabs.Add(resourceTab);
                SelectedTab = resourceTab;
            }

            AppendOutput($"Opened resource tab: {treeItem.Name}");
        }

        public void SelectTreeItem(ResourceTreeItemViewModel treeItem)
        {
            ArgumentNullException.ThrowIfNull(treeItem);

            _selectedTreeItemKey = treeItem.TreePathKey;

            foreach (var root in ResourceTree)
            {
                SetSelectionState(root, treeItem.TreePathKey);
            }

            foreach (var roomEditor in OpenTabs
                         .Select(static tab => tab.EditorContent)
                         .OfType<RoomEditorViewModel>())
            {
                roomEditor.NotifyPlacementSourceChanged();
            }
        }

        public void ToggleTreeItemExpansion(ResourceTreeItemViewModel treeItem)
        {
            ArgumentNullException.ThrowIfNull(treeItem);

            if (!treeItem.HasChildren)
            {
                return;
            }

            treeItem.IsExpanded = !treeItem.IsExpanded;
        }

        public void CreateOrInsertResource(ResourceTreeItemViewModel targetItem)
        {
            ArgumentNullException.ThrowIfNull(targetItem);

            if (!targetItem.CanCreateResource)
            {
                return;
            }

            var project = EnsureCurrentProject();
            EnsureProjectResourceRoots(project);

            var kind = targetItem.Kind;
            var resourceName = GenerateUniqueResourceName(project, kind);
            var resource = CreateResource(kind, resourceName);
            var resourceNode = new ProjectResourceTreeNode
            {
                Name = resource.Name,
                Kind = kind,
                Resource = resource,
            };

            if (resource is DataFile dataFile)
            {
                dataFile.FileName = BuildDataFilePath(targetItem, dataFile.Name);
                dataFile.OriginalName = dataFile.Name;
            }

            if (targetItem.IsFolder)
            {
                targetItem.IsExpanded = true;
            }

            InsertResourceNode(targetItem, resourceNode);
            SyncResourceListFromTree(project, kind);
            RebuildResourceTree(project);

            var createdTreeItem = FindTreeItem(kind, resourceNode.Resource?.Name ?? resourceName);
            if (createdTreeItem is not null)
            {
                SelectTreeItem(createdTreeItem);
                OpenResourceTab(createdTreeItem);
            }

            AppendOutput($"{(targetItem.IsFolder ? "Created" : "Inserted")} resource: {resource.Name}");
        }

        public void CreateOrInsertFolder(ResourceTreeItemViewModel targetItem)
        {
            ArgumentNullException.ThrowIfNull(targetItem);

            if (!targetItem.CanCreateFolder)
            {
                return;
            }

            var project = EnsureCurrentProject();
            EnsureProjectResourceRoots(project);

            var siblingContainer = targetItem.IsFolder
                ? targetItem.SourceNode
                : targetItem.Parent?.SourceNode;

            if (siblingContainer is null)
            {
                throw new InvalidOperationException("Cannot determine folder insertion container.");
            }

            var folderName = GenerateUniqueFolderName(siblingContainer);
            var folderNode = new ProjectResourceTreeNode
            {
                Name = folderName,
                Kind = targetItem.Kind,
            };

            if (targetItem.IsFolder)
            {
                targetItem.IsExpanded = true;
            }

            InsertResourceNode(targetItem, folderNode);
            RebuildResourceTree(project);

            var createdFolderItem = FindTreeItemByPathKey(folderNode, targetItem);
            if (createdFolderItem is not null)
            {
                SelectTreeItem(createdFolderItem);
            }

            AppendOutput($"{(targetItem.IsFolder ? "Created" : "Inserted")} folder: {folderName}");
        }

        public void DeleteTreeItem(ResourceTreeItemViewModel targetItem)
        {
            ArgumentNullException.ThrowIfNull(targetItem);

            if (!targetItem.CanDelete || targetItem.SourceNode is null)
            {
                return;
            }

            var project = EnsureCurrentProject();
            EnsureProjectResourceRoots(project);

            var parentNode = targetItem.Parent?.SourceNode;
            if (parentNode is null)
            {
                return;
            }

            var removedResourceKeys = CollectResourceKeys(targetItem.SourceNode, targetItem.Kind);
            parentNode.Children.Remove(targetItem.SourceNode);
            SyncResourceListFromTree(project, targetItem.Kind);
            RemoveOpenTabs(removedResourceKeys);

            _selectedTreeItemKey = targetItem.Parent?.TreePathKey;
            RebuildResourceTree(project);

            if (targetItem.Parent is not null)
            {
                var parentTreeItem = FindTreeItemByPathKey(targetItem.Parent.TreePathKey);
                if (parentTreeItem is not null)
                {
                    SelectTreeItem(parentTreeItem);
                }
            }

            AppendOutput($"{(targetItem.IsFolder ? "Deleted folder" : "Deleted resource")}: {targetItem.Name}");
        }

        public void AppendOutput(string message)
        {
            var line = $"[{DateTime.Now:HH:mm:ss}] {message}";
            OutputText = string.IsNullOrWhiteSpace(OutputText)
                ? line
                : $"{OutputText}{Environment.NewLine}{line}";
        }

        private void CreateNewProjectShell(bool writeLog)
        {
            var project = new Project
            {
                Name = "Untitled Project",
            };
            EnsureProjectResourceRoots(project);
            _selectedTreeItemKey = null;

            CurrentProject = project;
            CurrentProjectFilePath = null;
            ProjectName = project.Name;

            RebuildResourceTree(project, preserveExpandedState: false);
            RebuildOpenTabs(project);

            if (writeLog)
            {
                AppendOutput("Created an empty project.");
            }
        }

        private void RebuildResourceTree(Project project, bool preserveExpandedState = true)
        {
            EnsureProjectResourceRoots(project);
            var expandedState = preserveExpandedState
                ? CaptureExpandedState()
                : new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            ResourceTree.Clear();

            foreach (var node in project.ResourceTree.Select(node => CreateTreeItem(node, isRoot: true, parent: null, expandedState)))
            {
                ResourceTree.Add(node);
            }
        }

        private void RebuildOpenTabs(Project project)
        {
            OpenTabs.Clear();
            SelectedTab = null;
        }

        private ResourceTreeItemViewModel CreateTreeItem(ProjectResourceTreeNode node, bool isRoot, ResourceTreeItemViewModel? parent, IReadOnlyDictionary<string, bool> expandedState)
        {
            var treeItem = new ResourceTreeItemViewModel(
                GetDisplayName(node, isRoot),
                node.Kind,
                node.Resource,
                node,
                parent);
            if (expandedState.TryGetValue(treeItem.TreePathKey, out var isExpanded))
            {
                treeItem.IsExpanded = isExpanded;
            }
            if (string.Equals(treeItem.TreePathKey, _selectedTreeItemKey, StringComparison.OrdinalIgnoreCase))
            {
                treeItem.IsSelected = true;
            }

            foreach (var child in node.Children)
            {
                treeItem.Children.Add(CreateTreeItem(child, isRoot: false, parent: treeItem, expandedState));
            }

            return treeItem;
        }

        private Dictionary<string, bool> CaptureExpandedState()
        {
            var expandedState = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

            foreach (var root in ResourceTree)
            {
                CaptureExpandedState(root, expandedState);
            }

            return expandedState;
        }

        private static void CaptureExpandedState(ResourceTreeItemViewModel item, Dictionary<string, bool> expandedState)
        {
            expandedState[item.TreePathKey] = item.IsExpanded;

            foreach (var child in item.Children)
            {
                CaptureExpandedState(child, expandedState);
            }
        }

        private object BuildEditorContent(ProjectResourceKind kind, Resource resource)
        {
            return kind switch
            {
                ProjectResourceKind.Sprite when resource is Sprite sprite
                    => new SpriteEditorViewModel(sprite, RefreshResourceVisuals, AppendOutput),
                ProjectResourceKind.Script when resource is Script script
                    => new ScriptEditorViewModel(script),
                ProjectResourceKind.Sound when resource is Sound sound
                    => new SoundEditorViewModel(sound, AppendOutput),
                ProjectResourceKind.Font when resource is Font font
                    => new FontEditorViewModel(font),
                ProjectResourceKind.Object when resource is GameObject gameObject
                    => new ObjectEditorViewModel(EnsureCurrentProject(), gameObject, RefreshResourceVisuals, AppendOutput),
                ProjectResourceKind.Timeline when resource is Timeline timeline
                    => new TimelineEditorViewModel(timeline, AppendOutput),
                ProjectResourceKind.Room when resource is Room room
                    => new RoomEditorViewModel(EnsureCurrentProject(), room, AppendOutput, GetSelectedTreeObjectForRoomPlacement),
                ProjectResourceKind.Background when resource is Background background
                    => new BackgroundEditorViewModel(background, RefreshResourceVisuals, AppendOutput),
                _ => new ResourceSummaryEditorViewModel(
                    resource.Name,
                    GetResourceSubtitle(kind),
                    BuildResourceSummary(kind, resource))
            };
        }

        public void RefreshResourceVisuals(Resource resource)
        {
            foreach (var root in ResourceTree)
            {
                RefreshResourceVisuals(root, resource);
            }
        }

        private static void RefreshResourceVisuals(ResourceTreeItemViewModel item, Resource resource)
        {
            if (ReferenceEquals(item.Resource, resource)
                || item.Resource is GameObject gameObject && ReferenceEquals(gameObject.Sprite, resource))
            {
                item.NotifyIconChanged();
            }

            foreach (var child in item.Children)
            {
                RefreshResourceVisuals(child, resource);
            }
        }

        private static string GetResourceSubtitle(ProjectResourceKind kind)
        {
            return kind switch
            {
                ProjectResourceKind.Sprite => "Sprite Resource",
                ProjectResourceKind.Sound => "Sound Resource",
                ProjectResourceKind.Background => "Background Resource",
                ProjectResourceKind.Path => "Path Resource",
                ProjectResourceKind.Script => "Script Resource",
                ProjectResourceKind.Shader => "Shader Resource",
                ProjectResourceKind.Font => "Font Resource",
                ProjectResourceKind.Object => "Object Resource",
                ProjectResourceKind.Timeline => "Timeline Resource",
                ProjectResourceKind.Room => "Room Resource",
                ProjectResourceKind.DataFile => "Data File Resource",
                ProjectResourceKind.Extension => "Extension Resource",
                _ => "Resource"
            };
        }

        private GameObject? GetSelectedTreeObjectForRoomPlacement()
        {
            if (string.IsNullOrWhiteSpace(_selectedTreeItemKey))
            {
                return null;
            }

            var selectedTreeItem = FindTreeItemByPathKey(_selectedTreeItemKey);
            return selectedTreeItem?.Kind == ProjectResourceKind.Object
                ? selectedTreeItem.Resource as GameObject
                : null;
        }

        private static string BuildResourceSummary(ProjectResourceKind kind, Resource resource)
        {
            return kind switch
            {
                ProjectResourceKind.Sprite when resource is Sprite sprite => string.Join(Environment.NewLine,
                [
                    $"Name: {sprite.Name}",
                    $"Frames: {sprite.Frames.Count}",
                    $"Size: {sprite.Width} x {sprite.Height}",
                    $"Origin: ({sprite.XOrigin}, {sprite.YOrigin})",
                    "Sprite preview and property editing can be added here later."
                ]),
                ProjectResourceKind.Sound when resource is Sound sound => string.Join(Environment.NewLine,
                [
                    $"Name: {sound.Name}",
                    $"Extension: {sound.Extension}",
                    $"Audio Group: {sound.AudioGroup}",
                    $"Preload: {(sound.Preload ? "Yes" : "No")}",
                    "Sound parameter editing and preview playback can be added here later."
                ]),
                ProjectResourceKind.Background when resource is Background background => string.Join(Environment.NewLine,
                [
                    $"Name: {background.Name}",
                    $"Size: {background.Width} x {background.Height}",
                    $"Tileset: {(background.IsTileset ? "Yes" : "No")}",
                    "Background preview and property editing can be added here later."
                ]),
                ProjectResourceKind.Path when resource is GamePath path => string.Join(Environment.NewLine,
                [
                    $"Name: {path.Name}",
                    $"Point Count: {path.Points.Count}",
                    $"Closed: {(path.Closed ? "Yes" : "No")}",
                    "A path editor can be added here later."
                ]),
                ProjectResourceKind.Script when resource is Script script => string.Join(Environment.NewLine,
                [
                    $"Name: {script.Name}",
                    $"Code Length: {script.SourceCode.Length} characters",
                    "A script code editor can be added here later."
                ]),
                ProjectResourceKind.Shader when resource is Shader shader => string.Join(Environment.NewLine,
                [
                    $"Name: {shader.Name}",
                    $"Project Type: {shader.ProjectType}",
                    $"Vertex Length: {shader.VertexSource.Length} characters",
                    $"Fragment Length: {shader.FragmentSource.Length} characters",
                    "A shader editor can be added here later."
                ]),
                ProjectResourceKind.Font when resource is Font font => string.Join(Environment.NewLine,
                [
                    $"Name: {font.Name}",
                    $"Font: {font.FontName}",
                    $"Size: {font.Size}",
                    $"Glyph Count: {font.Glyphs.Count}",
                    "Font preview and property editing can be added here later."
                ]),
                ProjectResourceKind.Object when resource is GameObject gameObject => string.Join(Environment.NewLine,
                [
                    $"Name: {gameObject.Name}",
                    $"Event Count: {gameObject.Events.Count}",
                    $"Visible: {(gameObject.Visible ? "Yes" : "No")}",
                    $"Persistent: {(gameObject.Persistent ? "Yes" : "No")}",
                    "An object event editor can be added here later."
                ]),
                ProjectResourceKind.Timeline when resource is Timeline timeline => string.Join(Environment.NewLine,
                [
                    $"Name: {timeline.Name}",
                    $"Moment Count: {timeline.Moments.Count}",
                    "A timeline editor can be added here later."
                ]),
                ProjectResourceKind.Room when resource is Room room => string.Join(Environment.NewLine,
                [
                    $"Name: {room.Name}",
                    $"Size: {room.Width} x {room.Height}",
                    $"Instance Count: {room.Instances.Count}",
                    $"Tile Count: {room.Tiles.Count}",
                    "A room editor can be added here later."
                ]),
                ProjectResourceKind.DataFile when resource is DataFile dataFile => string.Join(Environment.NewLine,
                [
                    $"Name: {dataFile.Name}",
                    $"File Name: {dataFile.FileName}",
                    $"Size: {(dataFile.Size == 0 && dataFile.RawData is not null ? dataFile.RawData.Length : dataFile.Size)} bytes",
                    "Data file property editing can be added here later."
                ]),
                ProjectResourceKind.Extension when resource is Extension extension => string.Join(Environment.NewLine,
                [
                    $"Name: {extension.Name}",
                    $"Author: {extension.Author}",
                    $"Include Files: {extension.Includes.Count}",
                    $"Package Files: {extension.PackageFiles.Count}",
                    "An extension editor can be added here later."
                ]),
                _ => $"Name: {resource.Name}{Environment.NewLine}A dedicated resource editor can be added here later."
            };
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
                _ => []
            };
        }

        private static string GetDisplayName(ProjectResourceTreeNode node, bool isRoot)
        {
            if (!isRoot)
            {
                return node.Name;
            }

            return node.Kind switch
            {
                ProjectResourceKind.Sprite => "Sprites",
                ProjectResourceKind.Sound => "Sounds",
                ProjectResourceKind.Background => "Backgrounds",
                ProjectResourceKind.Path => "Paths",
                ProjectResourceKind.Script => "Scripts",
                ProjectResourceKind.Shader => "Shaders",
                ProjectResourceKind.Font => "Fonts",
                ProjectResourceKind.Object => "Objects",
                ProjectResourceKind.Timeline => "Timelines",
                ProjectResourceKind.Room => "Rooms",
                ProjectResourceKind.DataFile => "Datafiles",
                ProjectResourceKind.Extension => "Extensions",
                _ => node.Name
            };
        }

        private static void EnsureProjectResourceRoots(Project project)
        {
            foreach (var (kind, _) in DefaultResourceRoots)
            {
                if (project.ResourceTree.Any(node => node.Kind == kind))
                {
                    continue;
                }

                project.ResourceTree.Add(new ProjectResourceTreeNode
                {
                    Name = GetRootNodeName(kind),
                    Kind = kind,
                });
            }

            project.ResourceTree.Sort(static (left, right) => GetRootSortOrder(left.Kind).CompareTo(GetRootSortOrder(right.Kind)));
        }

        private static int GetRootSortOrder(ProjectResourceKind kind)
        {
            for (var index = 0; index < DefaultResourceRoots.Length; index++)
            {
                if (DefaultResourceRoots[index].Kind == kind)
                {
                    return index;
                }
            }

            return int.MaxValue;
        }

        private static string GetRootNodeName(ProjectResourceKind kind)
        {
            return kind switch
            {
                ProjectResourceKind.Sprite => "sprites",
                ProjectResourceKind.Sound => "sound",
                ProjectResourceKind.Background => "background",
                ProjectResourceKind.Path => "paths",
                ProjectResourceKind.Script => "scripts",
                ProjectResourceKind.Shader => "shaders",
                ProjectResourceKind.Font => "fonts",
                ProjectResourceKind.Object => "objects",
                ProjectResourceKind.Timeline => "timelines",
                ProjectResourceKind.Room => "rooms",
                ProjectResourceKind.DataFile => "datafiles",
                ProjectResourceKind.Extension => "extensions",
                _ => "resources"
            };
        }

        private static string GetResourceTypeName(ProjectResourceKind kind)
        {
            return kind switch
            {
                ProjectResourceKind.Sprite => "Sprite",
                ProjectResourceKind.Sound => "Sound",
                ProjectResourceKind.Background => "Background",
                ProjectResourceKind.Path => "Path",
                ProjectResourceKind.Script => "Script",
                ProjectResourceKind.Shader => "Shader",
                ProjectResourceKind.Font => "Font",
                ProjectResourceKind.Object => "Object",
                ProjectResourceKind.Timeline => "Timeline",
                ProjectResourceKind.Room => "Room",
                ProjectResourceKind.DataFile => "DataFile",
                ProjectResourceKind.Extension => "Extension",
                _ => "Resource"
            };
        }

        private static void SetSelectionState(ResourceTreeItemViewModel item, string selectedTreePathKey)
        {
            item.IsSelected = string.Equals(item.TreePathKey, selectedTreePathKey, StringComparison.OrdinalIgnoreCase);

            foreach (var child in item.Children)
            {
                SetSelectionState(child, selectedTreePathKey);
            }
        }

        private static string GetResourceBaseName(ProjectResourceKind kind)
        {
            return kind switch
            {
                ProjectResourceKind.Sprite => "sprite",
                ProjectResourceKind.Sound => "sound",
                ProjectResourceKind.Background => "background",
                ProjectResourceKind.Path => "path",
                ProjectResourceKind.Script => "script",
                ProjectResourceKind.Shader => "shader",
                ProjectResourceKind.Font => "font",
                ProjectResourceKind.Object => "object",
                ProjectResourceKind.Timeline => "timeline",
                ProjectResourceKind.Room => "room",
                ProjectResourceKind.DataFile => "datafile",
                ProjectResourceKind.Extension => "extension",
                _ => "resource"
            };
        }

        private static ProjectResourceTreeNode GetRootNode(Project project, ProjectResourceKind kind)
        {
            return project.ResourceTree.First(node => node.Kind == kind);
        }

        private static string GenerateUniqueResourceName(Project project, ProjectResourceKind kind)
        {
            var baseName = GetResourceBaseName(kind);
            var existingNames = new HashSet<string>(
                GetResourcesByKind(project, kind)
                    .Select(static resource => resource.Name),
                StringComparer.OrdinalIgnoreCase);

            var nextIndex = existingNames.Count;
            while (true)
            {
                var candidate = $"{baseName}{nextIndex}";
                if (!existingNames.Contains(candidate))
                {
                    return candidate;
                }

                nextIndex++;
            }
        }

        private static string GenerateUniqueFolderName(ProjectResourceTreeNode containerNode)
        {
            var existingNames = new HashSet<string>(
                containerNode.Children
                    .Where(static node => node.Resource is null)
                    .Select(static node => node.Name),
                StringComparer.OrdinalIgnoreCase);

            var nextIndex = existingNames.Count;
            while (true)
            {
                var candidate = $"folder{nextIndex}";
                if (!existingNames.Contains(candidate))
                {
                    return candidate;
                }

                nextIndex++;
            }
        }

        private static HashSet<string> CollectResourceKeys(ProjectResourceTreeNode node, ProjectResourceKind kind)
        {
            var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var resourceNode in EnumerateSelfAndDescendantResourceNodes(node))
            {
                if (resourceNode.Resource is null)
                {
                    continue;
                }

                keys.Add(ResourceTreeItemViewModel.BuildResourceKey(kind, resourceNode.Resource, resourceNode.Name));
            }

            return keys;
        }

        private static Resource CreateResource(ProjectResourceKind kind, string name)
        {
            return kind switch
            {
                ProjectResourceKind.Sprite => new Sprite { Name = name },
                ProjectResourceKind.Sound => new Sound { Name = name },
                ProjectResourceKind.Background => new Background { Name = name },
                ProjectResourceKind.Path => new GamePath { Name = name },
                ProjectResourceKind.Script => new Script { Name = name },
                ProjectResourceKind.Shader => new Shader { Name = name },
                ProjectResourceKind.Font => new Font { Name = name },
                ProjectResourceKind.Object => new GameObject { Name = name },
                ProjectResourceKind.Timeline => new Timeline { Name = name },
                ProjectResourceKind.Room => new Room { Name = name },
                ProjectResourceKind.DataFile => new DataFile { Name = name },
                ProjectResourceKind.Extension => new Extension { Name = name },
                _ => throw new InvalidOperationException($"Unsupported resource kind: {kind}.")
            };
        }

        private static void InsertResourceNode(ResourceTreeItemViewModel targetItem, ProjectResourceTreeNode resourceNode)
        {
            if (targetItem.SourceNode is null)
            {
                throw new InvalidOperationException("Resource tree node is not linked to the project tree.");
            }

            if (targetItem.IsFolder)
            {
                targetItem.SourceNode.Children.Add(resourceNode);
                return;
            }

            var parentNode = targetItem.Parent?.SourceNode
                ?? throw new InvalidOperationException("Cannot insert a resource without a parent folder.");
            var insertionIndex = parentNode.Children.IndexOf(targetItem.SourceNode);
            if (insertionIndex < 0)
            {
                parentNode.Children.Add(resourceNode);
                return;
            }

            parentNode.Children.Insert(insertionIndex, resourceNode);
        }

        private void SyncResourceListFromTree(Project project, ProjectResourceKind kind)
        {
            var rootNode = GetRootNode(project, kind);

            switch (kind)
            {
                case ProjectResourceKind.Sprite:
                    SyncResourceList(rootNode, project.Sprites);
                    break;
                case ProjectResourceKind.Sound:
                    SyncResourceList(rootNode, project.Sounds);
                    break;
                case ProjectResourceKind.Background:
                    SyncResourceList(rootNode, project.Backgrounds);
                    break;
                case ProjectResourceKind.Path:
                    SyncResourceList(rootNode, project.Paths);
                    break;
                case ProjectResourceKind.Script:
                    SyncResourceList(rootNode, project.Scripts);
                    break;
                case ProjectResourceKind.Shader:
                    SyncResourceList(rootNode, project.Shaders);
                    break;
                case ProjectResourceKind.Font:
                    SyncResourceList(rootNode, project.Fonts);
                    break;
                case ProjectResourceKind.Object:
                    SyncResourceList(rootNode, project.Objects);
                    break;
                case ProjectResourceKind.Timeline:
                    SyncResourceList(rootNode, project.Timelines);
                    break;
                case ProjectResourceKind.Room:
                    SyncResourceList(rootNode, project.Rooms);
                    break;
                case ProjectResourceKind.DataFile:
                    SyncResourceList(rootNode, project.DataFiles);
                    break;
                case ProjectResourceKind.Extension:
                    SyncResourceList(rootNode, project.Extensions);
                    break;
            }
        }

        private static void SyncResourceList<TResource>(ProjectResourceTreeNode rootNode, List<TResource> resources)
            where TResource : Resource
        {
            var orderedResources = EnumerateResourceNodes(rootNode)
                .Select(static node => node.Resource)
                .OfType<TResource>()
                .ToList();

            resources.Clear();
            resources.AddRange(orderedResources);
        }

        private static IEnumerable<ProjectResourceTreeNode> EnumerateResourceNodes(ProjectResourceTreeNode node)
        {
            foreach (var child in node.Children)
            {
                if (child.Resource is not null)
                {
                    yield return child;
                }

                foreach (var descendant in EnumerateResourceNodes(child))
                {
                    yield return descendant;
                }
            }
        }

        private static IEnumerable<ProjectResourceTreeNode> EnumerateSelfAndDescendantResourceNodes(ProjectResourceTreeNode node)
        {
            if (node.Resource is not null)
            {
                yield return node;
            }

            foreach (var child in node.Children)
            {
                foreach (var descendant in EnumerateSelfAndDescendantResourceNodes(child))
                {
                    yield return descendant;
                }
            }
        }

        private string BuildDataFilePath(ResourceTreeItemViewModel targetItem, string fileName)
        {
            var segments = new Stack<string>();
            var current = targetItem.IsFolder ? targetItem : targetItem.Parent;

            while (current is not null)
            {
                if (current.Parent is not null || current.Kind == ProjectResourceKind.DataFile)
                {
                    segments.Push(current.SourceNode?.Name ?? current.Name);
                }

                current = current.Parent;
            }

            return segments.Count == 0
                ? fileName
                : Path.Combine(segments.ToArray().Concat([fileName]).ToArray());
        }

        private ResourceTreeItemViewModel? FindTreeItem(ProjectResourceKind kind, string resourceName)
        {
            foreach (var root in ResourceTree)
            {
                var found = FindTreeItem(root, kind, resourceName);
                if (found is not null)
                {
                    return found;
                }
            }

            return null;
        }

        private ResourceTreeItemViewModel? FindTreeItemByPathKey(ProjectResourceTreeNode node, ResourceTreeItemViewModel targetItem)
        {
            var pathKey = BuildProjectedTreePathKey(node, targetItem);
            if (string.IsNullOrWhiteSpace(pathKey))
            {
                return null;
            }

            foreach (var root in ResourceTree)
            {
                var found = FindTreeItemByPathKey(root, pathKey);
                if (found is not null)
                {
                    return found;
                }
            }

            return null;
        }

        private ResourceTreeItemViewModel? FindTreeItemByPathKey(string pathKey)
        {
            foreach (var root in ResourceTree)
            {
                var found = FindTreeItemByPathKey(root, pathKey);
                if (found is not null)
                {
                    return found;
                }
            }

            return null;
        }

        private static ResourceTreeItemViewModel? FindTreeItem(ResourceTreeItemViewModel current, ProjectResourceKind kind, string resourceName)
        {
            if (!current.IsFolder
                && current.Kind == kind
                && string.Equals(current.Name, resourceName, StringComparison.OrdinalIgnoreCase))
            {
                return current;
            }

            foreach (var child in current.Children)
            {
                var found = FindTreeItem(child, kind, resourceName);
                if (found is not null)
                {
                    return found;
                }
            }

            return null;
        }

        private static ResourceTreeItemViewModel? FindTreeItemByPathKey(ResourceTreeItemViewModel current, string pathKey)
        {
            if (string.Equals(current.TreePathKey, pathKey, StringComparison.OrdinalIgnoreCase))
            {
                return current;
            }

            foreach (var child in current.Children)
            {
                var found = FindTreeItemByPathKey(child, pathKey);
                if (found is not null)
                {
                    return found;
                }
            }

            return null;
        }

        private void RemoveOpenTabs(HashSet<string> removedResourceKeys)
        {
            if (removedResourceKeys.Count == 0)
            {
                return;
            }

            for (var index = OpenTabs.Count - 1; index >= 0; index--)
            {
                var tab = OpenTabs[index];
                if (string.IsNullOrWhiteSpace(tab.ResourceKey) || !removedResourceKeys.Contains(tab.ResourceKey))
                {
                    continue;
                }

                if (ReferenceEquals(SelectedTab, tab))
                {
                    SelectedTab = null;
                }

                OpenTabs.RemoveAt(index);
            }
        }

        private static string? BuildProjectedTreePathKey(ProjectResourceTreeNode newNode, ResourceTreeItemViewModel targetItem)
        {
            var selfKey = $"folder:{targetItem.Kind}:{newNode.Name}";
            if (targetItem.IsFolder)
            {
                return targetItem.TreePathKey + "/" + selfKey;
            }

            return targetItem.Parent is null
                ? selfKey
                : targetItem.Parent.TreePathKey + "/" + selfKey;
        }

        private string? _selectedTreeItemKey;

        private static string GetProjectNameFromPath(string projectFilePath, string fallbackName)
        {
            var fileName = Path.GetFileName(projectFilePath);
            if (fileName.EndsWith(".project.gmx", StringComparison.OrdinalIgnoreCase))
            {
                return fileName[..^".project.gmx".Length];
            }

            var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(projectFilePath);
            return string.IsNullOrWhiteSpace(fileNameWithoutExtension)
                ? fallbackName
                : fileNameWithoutExtension;
        }
    }

    public partial class ResourceTreeItemViewModel : ObservableObject
    {
        private static readonly IBrush SelectedRowBackgroundBrush = new SolidColorBrush(Color.Parse("#FFDCEBFF"));
        private static readonly IBrush SelectedRowBorderBrush = new SolidColorBrush(Color.Parse("#FFB6D1F7"));
        private static readonly IBrush TransparentBrush = new SolidColorBrush(Colors.Transparent);
        private static readonly IBrush DefaultRowForegroundBrush = new SolidColorBrush(Color.Parse("#FF1F1F1F"));

        public string Name { get; }

        public ProjectResourceKind Kind { get; }

        public Resource? Resource { get; }

        public ProjectResourceTreeNode? SourceNode { get; }

        public ResourceTreeItemViewModel? Parent { get; }

        public ObservableCollection<ResourceTreeItemViewModel> Children { get; }

        public bool IsFolder => Resource is null;

        public string ResourceKey { get; }

        public string TreePathKey { get; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ShowChildren))]
        [NotifyPropertyChangedFor(nameof(ExpandGlyph))]
        [NotifyPropertyChangedFor(nameof(TreeIconSource))]
        private bool isExpanded;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(RowBackground))]
        [NotifyPropertyChangedFor(nameof(RowBorderBrush))]
        [NotifyPropertyChangedFor(nameof(RowForeground))]
        [NotifyPropertyChangedFor(nameof(RowFontWeight))]
        private bool isSelected;

        public bool CanCreateResource => Kind != ProjectResourceKind.Unknown;

        public bool CanCreateFolder => Kind != ProjectResourceKind.Unknown
            && Kind != ProjectResourceKind.Extension;

        public bool CanDelete => Parent is not null;

        public string CreateOrInsertMenuHeader => $"{(IsFolder ? "Create" : "Insert")} {GetMenuResourceTypeName(Kind)}";

        public string CreateOrInsertFolderMenuHeader => IsFolder ? "Create Folder" : "Insert Folder";

        public string DeleteMenuHeader => IsFolder ? "Delete Folder" : "Delete";

        public bool HasChildren => Children.Count > 0;

        public bool ShowChildren => HasChildren && IsExpanded;

        public string ExpandGlyph => IsExpanded ? "▾" : "▸";

        public IImage TreeIconSource => Resource switch
        {
            Sprite sprite => sprite.Frames
                .OrderBy(static frame => frame.Index)
                .Select(static frame => frame.Bitmap)
                .FirstOrDefault(static bitmap => bitmap is not null)
                ?? AppIconCatalog.GetTreeIcon(Kind, isFolder: false, isExpanded: false),
            Background background => background.Bitmap
                ?? AppIconCatalog.GetTreeIcon(Kind, isFolder: false, isExpanded: false),
            GameObject gameObject => gameObject.Sprite?.Frames
                .OrderBy(static frame => frame.Index)
                .Select(static frame => frame.Bitmap)
                .FirstOrDefault(static bitmap => bitmap is not null)
                ?? AppIconCatalog.GetTreeIcon(Kind, isFolder: false, isExpanded: false),
            _ => AppIconCatalog.GetTreeIcon(Kind, IsFolder, IsExpanded),
        };

        public IBrush RowBackground => IsSelected ? SelectedRowBackgroundBrush : TransparentBrush;

        public IBrush RowBorderBrush => IsSelected ? SelectedRowBorderBrush : TransparentBrush;

        public IBrush RowForeground => DefaultRowForegroundBrush;

        public FontWeight RowFontWeight => IsSelected ? FontWeight.SemiBold : FontWeight.Normal;

        public ResourceTreeItemViewModel(string name)
            : this(name, [])
        {
        }

        public ResourceTreeItemViewModel(string name, IEnumerable<ResourceTreeItemViewModel> children)
            : this(name, ProjectResourceKind.Unknown, null, null, null, children)
        {
        }

        public ResourceTreeItemViewModel(string name, ProjectResourceKind kind, Resource resource)
            : this(name, kind, resource, null, null, [])
        {
        }

        public ResourceTreeItemViewModel(string name, ProjectResourceKind kind, Resource? resource, ProjectResourceTreeNode? sourceNode, ResourceTreeItemViewModel? parent)
            : this(name, kind, resource, sourceNode, parent, [])
        {
        }

        public ResourceTreeItemViewModel(string name, ProjectResourceKind kind, Resource? resource, ProjectResourceTreeNode? sourceNode, ResourceTreeItemViewModel? parent, IEnumerable<ResourceTreeItemViewModel> children)
        {
            Name = name;
            Kind = kind;
            Resource = resource;
            SourceNode = sourceNode;
            Parent = parent;
            Children = new ObservableCollection<ResourceTreeItemViewModel>(children);
            ResourceKey = BuildResourceKey(kind, resource, name);
            TreePathKey = BuildTreePathKey(parent, kind, sourceNode, resource, name);
        }

        public void NotifyIconChanged()
        {
            OnPropertyChanged(nameof(TreeIconSource));
        }

        internal static string BuildResourceKey(ProjectResourceKind kind, Resource? resource, string fallbackName)
        {
            var identity = resource switch
            {
                DataFile dataFile when !string.IsNullOrWhiteSpace(dataFile.FileName) => dataFile.FileName,
                Resource typedResource when !string.IsNullOrWhiteSpace(typedResource.Name) => typedResource.Name,
                _ => fallbackName
            };

            return $"{kind}:{identity}";
        }

        private static string BuildTreePathKey(ResourceTreeItemViewModel? parent, ProjectResourceKind kind, ProjectResourceTreeNode? sourceNode, Resource? resource, string name)
        {
            var selfKey = sourceNode?.Resource switch
            {
                DataFile dataFile when !string.IsNullOrWhiteSpace(dataFile.FileName) => $"resource:{kind}:{dataFile.FileName}",
                Resource typedResource when !string.IsNullOrWhiteSpace(typedResource.Name) => $"resource:{kind}:{typedResource.Name}",
                _ when sourceNode?.Resource is null => $"folder:{kind}:{sourceNode?.Name ?? name}",
                _ => $"node:{kind}:{name}",
            };

            return parent is null
                ? selfKey
                : $"{parent.TreePathKey}/{selfKey}";
        }

        private static string GetMenuResourceTypeName(ProjectResourceKind kind)
        {
            return kind switch
            {
                ProjectResourceKind.Sprite => "Sprite",
                ProjectResourceKind.Sound => "Sound",
                ProjectResourceKind.Background => "Background",
                ProjectResourceKind.Path => "Path",
                ProjectResourceKind.Script => "Script",
                ProjectResourceKind.Shader => "Shader",
                ProjectResourceKind.Font => "Font",
                ProjectResourceKind.Object => "Object",
                ProjectResourceKind.Timeline => "Timeline",
                ProjectResourceKind.Room => "Room",
                ProjectResourceKind.DataFile => "DataFile",
                ProjectResourceKind.Extension => "Extension",
                _ => "Resource"
            };
        }
    }

    public partial class EditorTabViewModel : ObservableObject
    {
        [ObservableProperty]
        private string header;

        [ObservableProperty]
        private object? editorContent;

        [ObservableProperty]
        private ProjectResourceKind resourceKind;

        [ObservableProperty]
        private string? resourceKey;

        public bool CanBeReplaced { get; private set; }

        public EditorTabViewModel(string header, object? editorContent)
            : this(header, editorContent, ProjectResourceKind.Unknown, null, canBeReplaced: false)
        {
        }

        public EditorTabViewModel(string header, object? editorContent, ProjectResourceKind resourceKind, string? resourceKey, bool canBeReplaced)
        {
            this.header = header;
            this.editorContent = editorContent;
            this.resourceKind = resourceKind;
            this.resourceKey = resourceKey;
            CanBeReplaced = canBeReplaced;
        }

        public bool MatchesResource(ProjectResourceKind kind, string resourceKey)
        {
            return ResourceKind == kind
                && string.Equals(ResourceKey, resourceKey, StringComparison.OrdinalIgnoreCase);
        }

        public void ReplaceWith(string header, ProjectResourceKind resourceKind, string? resourceKey, object? editorContent)
        {
            Header = header;
            EditorContent = editorContent;
            ResourceKind = resourceKind;
            ResourceKey = resourceKey;
            CanBeReplaced = true;
        }

        public static EditorTabViewModel CreateResourceTab(string header, ProjectResourceKind resourceKind, string? resourceKey, object? editorContent)
        {
            return new EditorTabViewModel(header, editorContent, resourceKind, resourceKey, canBeReplaced: true);
        }
    }
}
