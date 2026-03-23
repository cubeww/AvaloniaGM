using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using Avalonia.Media;
using AvaloniaGM.Models;

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
        private string projectName = "未命名项目";

        [ObservableProperty]
        private EditorTabViewModel? selectedTab;

        [ObservableProperty]
        private string outputText = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(WindowTitle))]
        private string? currentProjectFilePath;

        [ObservableProperty]
        private Project? currentProject;

        public string WindowTitle => $"AvaloniaGM - {ProjectName}";

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

        [RelayCommand]
        private void RunProject()
        {
            AppendOutput("运行项目按钮已触发，编译与运行流程待实现。");
        }

        [RelayCommand]
        private void ShowHelp()
        {
            AppendOutput("帮助菜单已触发，关于窗口与文档入口待实现。");
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

            AppendOutput($"项目已打开：{fullPath}");
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
                ? $"项目已另存为：{fullPath}"
                : $"项目已保存：{fullPath}");
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

            if (replacementTab is not null)
            {
                replacementTab.ReplaceWith(
                    treeItem.Name,
                    GetResourceSubtitle(treeItem.Kind),
                    BuildResourceSummary(treeItem.Kind, treeItem.Resource),
                    treeItem.Kind,
                    treeItem.ResourceKey);
                SelectedTab = replacementTab;
            }
            else
            {
                var resourceTab = EditorTabViewModel.CreateResourceTab(
                    treeItem.Name,
                    GetResourceSubtitle(treeItem.Kind),
                    BuildResourceSummary(treeItem.Kind, treeItem.Resource),
                    treeItem.Kind,
                    treeItem.ResourceKey);

                OpenTabs.Add(resourceTab);
                SelectedTab = resourceTab;
            }

            AppendOutput($"已打开资源标签页：{treeItem.Name}");
        }

        public void SelectTreeItem(ResourceTreeItemViewModel treeItem)
        {
            ArgumentNullException.ThrowIfNull(treeItem);

            _selectedTreeItemKey = treeItem.TreePathKey;

            foreach (var root in ResourceTree)
            {
                SetSelectionState(root, treeItem.TreePathKey);
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

            AppendOutput($"{(targetItem.IsFolder ? "已创建" : "已插入")}资源：{resource.Name}");
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

            AppendOutput($"{(targetItem.IsFolder ? "已创建" : "已插入")}文件夹：{folderName}");
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

            AppendOutput($"{(targetItem.IsFolder ? "已删除文件夹" : "已删除资源")}：{targetItem.Name}");
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
                Name = "未命名项目",
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
                AppendOutput("已创建空项目。");
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

        private static string GetResourceSubtitle(ProjectResourceKind kind)
        {
            return kind switch
            {
                ProjectResourceKind.Sprite => "Sprite 资源",
                ProjectResourceKind.Sound => "Sound 资源",
                ProjectResourceKind.Background => "Background 资源",
                ProjectResourceKind.Path => "Path 资源",
                ProjectResourceKind.Script => "Script 资源",
                ProjectResourceKind.Shader => "Shader 资源",
                ProjectResourceKind.Font => "Font 资源",
                ProjectResourceKind.Object => "Object 资源",
                ProjectResourceKind.Timeline => "Timeline 资源",
                ProjectResourceKind.Room => "Room 资源",
                ProjectResourceKind.DataFile => "Datafile 资源",
                ProjectResourceKind.Extension => "Extension 资源",
                _ => "资源"
            };
        }

        private static string BuildResourceSummary(ProjectResourceKind kind, Resource resource)
        {
            return kind switch
            {
                ProjectResourceKind.Sprite when resource is Sprite sprite => string.Join(Environment.NewLine,
                [
                    $"名称：{sprite.Name}",
                    $"帧数：{sprite.Frames.Count}",
                    $"尺寸：{sprite.Width} x {sprite.Height}",
                    $"原点：({sprite.XOrigin}, {sprite.YOrigin})",
                    "这里后续可以接入 Sprite 预览与属性编辑。"
                ]),
                ProjectResourceKind.Sound when resource is Sound sound => string.Join(Environment.NewLine,
                [
                    $"名称：{sound.Name}",
                    $"扩展名：{sound.Extension}",
                    $"音频组：{sound.AudioGroup}",
                    $"预加载：{(sound.Preload ? "是" : "否")}",
                    "这里后续可以接入 Sound 参数编辑与试听。"
                ]),
                ProjectResourceKind.Background when resource is Background background => string.Join(Environment.NewLine,
                [
                    $"名称：{background.Name}",
                    $"尺寸：{background.Width} x {background.Height}",
                    $"Tileset：{(background.IsTileset ? "是" : "否")}",
                    "这里后续可以接入 Background 预览与属性编辑。"
                ]),
                ProjectResourceKind.Path when resource is GamePath path => string.Join(Environment.NewLine,
                [
                    $"名称：{path.Name}",
                    $"点数量：{path.Points.Count}",
                    $"闭合：{(path.Closed ? "是" : "否")}",
                    "这里后续可以接入路径编辑器。"
                ]),
                ProjectResourceKind.Script when resource is Script script => string.Join(Environment.NewLine,
                [
                    $"名称：{script.Name}",
                    $"代码长度：{script.SourceCode.Length} 字符",
                    "这里后续可以接入脚本代码编辑器。"
                ]),
                ProjectResourceKind.Shader when resource is Shader shader => string.Join(Environment.NewLine,
                [
                    $"名称：{shader.Name}",
                    $"项目类型：{shader.ProjectType}",
                    $"Vertex 长度：{shader.VertexSource.Length} 字符",
                    $"Fragment 长度：{shader.FragmentSource.Length} 字符",
                    "这里后续可以接入 Shader 编辑器。"
                ]),
                ProjectResourceKind.Font when resource is Font font => string.Join(Environment.NewLine,
                [
                    $"名称：{font.Name}",
                    $"字体：{font.FontName}",
                    $"字号：{font.Size}",
                    $"字形数：{font.Glyphs.Count}",
                    "这里后续可以接入 Font 预览与属性编辑。"
                ]),
                ProjectResourceKind.Object when resource is GameObject gameObject => string.Join(Environment.NewLine,
                [
                    $"名称：{gameObject.Name}",
                    $"事件数：{gameObject.Events.Count}",
                    $"可见：{(gameObject.Visible ? "是" : "否")}",
                    $"持久：{(gameObject.Persistent ? "是" : "否")}",
                    "这里后续可以接入 Object 事件编辑器。"
                ]),
                ProjectResourceKind.Timeline when resource is Timeline timeline => string.Join(Environment.NewLine,
                [
                    $"名称：{timeline.Name}",
                    $"Moment 数量：{timeline.Moments.Count}",
                    "这里后续可以接入 Timeline 编辑器。"
                ]),
                ProjectResourceKind.Room when resource is Room room => string.Join(Environment.NewLine,
                [
                    $"名称：{room.Name}",
                    $"尺寸：{room.Width} x {room.Height}",
                    $"实例数：{room.Instances.Count}",
                    $"Tile 数：{room.Tiles.Count}",
                    "这里后续可以接入 Room 编辑器。"
                ]),
                ProjectResourceKind.DataFile when resource is DataFile dataFile => string.Join(Environment.NewLine,
                [
                    $"名称：{dataFile.Name}",
                    $"文件名：{dataFile.FileName}",
                    $"大小：{(dataFile.Size == 0 && dataFile.RawData is not null ? dataFile.RawData.Length : dataFile.Size)} 字节",
                    "这里后续可以接入 Datafile 属性编辑。"
                ]),
                ProjectResourceKind.Extension when resource is Extension extension => string.Join(Environment.NewLine,
                [
                    $"名称：{extension.Name}",
                    $"作者：{extension.Author}",
                    $"包含文件：{extension.Includes.Count}",
                    $"包文件：{extension.PackageFiles.Count}",
                    "这里后续可以接入 Extension 编辑器。"
                ]),
                _ => $"名称：{resource.Name}{Environment.NewLine}这里后续可以接入对应资源编辑器。"
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
        private string subtitle;

        [ObservableProperty]
        private string placeholderContent;

        [ObservableProperty]
        private ProjectResourceKind resourceKind;

        [ObservableProperty]
        private string? resourceKey;

        public bool CanBeReplaced { get; private set; }

        public EditorTabViewModel(string header, string subtitle, string placeholderContent)
            : this(header, subtitle, placeholderContent, ProjectResourceKind.Unknown, null, canBeReplaced: false)
        {
        }

        public EditorTabViewModel(string header, string subtitle, string placeholderContent, ProjectResourceKind resourceKind, string? resourceKey, bool canBeReplaced)
        {
            this.header = header;
            this.subtitle = subtitle;
            this.placeholderContent = placeholderContent;
            this.resourceKind = resourceKind;
            this.resourceKey = resourceKey;
            CanBeReplaced = canBeReplaced;
        }

        public bool MatchesResource(ProjectResourceKind kind, string resourceKey)
        {
            return ResourceKind == kind
                && string.Equals(ResourceKey, resourceKey, StringComparison.OrdinalIgnoreCase);
        }

        public void ReplaceWith(string header, string subtitle, string placeholderContent, ProjectResourceKind resourceKind, string? resourceKey)
        {
            Header = header;
            Subtitle = subtitle;
            PlaceholderContent = placeholderContent;
            ResourceKind = resourceKind;
            ResourceKey = resourceKey;
            CanBeReplaced = true;
        }

        public static EditorTabViewModel CreateResourceTab(string header, string subtitle, string placeholderContent, ProjectResourceKind resourceKind, string? resourceKey)
        {
            return new EditorTabViewModel(header, subtitle, placeholderContent, resourceKind, resourceKey, canBeReplaced: true);
        }
    }
}
