using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
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

            CurrentProject = project;
            CurrentProjectFilePath = fullPath;
            ProjectName = project.Name;

            RebuildResourceTree(project);
            RebuildOpenTabs(project);

            AppendOutput($"项目已打开：{fullPath}");
        }

        public void MarkProjectSaved(string projectFilePath, bool savedAs)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(projectFilePath);

            var fullPath = Path.GetFullPath(projectFilePath);
            var project = EnsureCurrentProject();

            project.Name = GetProjectNameFromPath(fullPath, project.Name);
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

            CurrentProject = project;
            CurrentProjectFilePath = null;
            ProjectName = project.Name;

            RebuildResourceTree(project);
            RebuildOpenTabs(project);

            if (writeLog)
            {
                AppendOutput("已创建空项目。");
            }
        }

        private void RebuildResourceTree(Project project)
        {
            ResourceTree.Clear();

            IEnumerable<ResourceTreeItemViewModel> nodes = project.ResourceTree.Count > 0
                ? project.ResourceTree.Select(static node => CreateTreeItem(node, isRoot: true))
                : BuildDefaultTree(project);

            foreach (var node in nodes)
            {
                ResourceTree.Add(node);
            }
        }

        private void RebuildOpenTabs(Project project)
        {
            OpenTabs.Clear();
            SelectedTab = null;
        }

        private static ResourceTreeItemViewModel CreateTreeItem(ProjectResourceTreeNode node, bool isRoot)
        {
            var children = node.Children.Select(static child => CreateTreeItem(child, isRoot: false));
            return new ResourceTreeItemViewModel(
                GetDisplayName(node, isRoot),
                node.Kind,
                node.Resource,
                children);
        }

        private static IEnumerable<ResourceTreeItemViewModel> BuildDefaultTree(Project project)
        {
            foreach (var (kind, displayName) in DefaultResourceRoots)
            {
                var children = GetResourcesByKind(project, kind)
                    .OrderBy(static resource => resource.Name, StringComparer.OrdinalIgnoreCase)
                    .Select(resource => new ResourceTreeItemViewModel(resource.Name, kind, resource));

                yield return new ResourceTreeItemViewModel(displayName, children);
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

    public class ResourceTreeItemViewModel
    {
        public string Name { get; }

        public ProjectResourceKind Kind { get; }

        public Resource? Resource { get; }

        public ObservableCollection<ResourceTreeItemViewModel> Children { get; }

        public bool IsFolder => Resource is null;

        public string ResourceKey { get; }

        public ResourceTreeItemViewModel(string name)
            : this(name, [])
        {
        }

        public ResourceTreeItemViewModel(string name, IEnumerable<ResourceTreeItemViewModel> children)
            : this(name, ProjectResourceKind.Unknown, null, children)
        {
        }

        public ResourceTreeItemViewModel(string name, ProjectResourceKind kind, Resource resource)
            : this(name, kind, resource, [])
        {
        }

        public ResourceTreeItemViewModel(string name, ProjectResourceKind kind, Resource? resource, IEnumerable<ResourceTreeItemViewModel> children)
        {
            Name = name;
            Kind = kind;
            Resource = resource;
            Children = new ObservableCollection<ResourceTreeItemViewModel>(children);
            ResourceKey = BuildResourceKey(kind, resource, name);
        }

        private static string BuildResourceKey(ProjectResourceKind kind, Resource? resource, string fallbackName)
        {
            var identity = resource switch
            {
                DataFile dataFile when !string.IsNullOrWhiteSpace(dataFile.FileName) => dataFile.FileName,
                Resource typedResource when !string.IsNullOrWhiteSpace(typedResource.Name) => typedResource.Name,
                _ => fallbackName
            };

            return $"{kind}:{identity}";
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
