using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using AvaloniaGM.Services;
using AvaloniaGM.ViewModels;

namespace AvaloniaGM.Views
{
    public partial class MainWindow : Window
    {
        private static readonly FilePickerFileType GameMakerProjectFileType = new("GameMaker Studio 1.4 Project")
        {
            Patterns = ["*.project.gmx"]
        };

        private readonly ProjectGmxSerializer _projectSerializer = new();

        public MainWindow()
        {
            InitializeComponent();
        }

        private MainWindowViewModel? ViewModel => DataContext as MainWindowViewModel;

        private async void OpenProjectMenuItem_OnClick(object? sender, RoutedEventArgs e)
        {
            await OpenProjectAsync();
        }

        private async void OpenProjectButton_OnClick(object? sender, RoutedEventArgs e)
        {
            await OpenProjectAsync();
        }

        private async void SaveProjectMenuItem_OnClick(object? sender, RoutedEventArgs e)
        {
            await SaveProjectAsync();
        }

        private async void SaveProjectButton_OnClick(object? sender, RoutedEventArgs e)
        {
            await SaveProjectAsync();
        }

        private async void SaveProjectAsMenuItem_OnClick(object? sender, RoutedEventArgs e)
        {
            await SaveProjectAsAsync();
        }

        private async Task OpenProjectAsync()
        {
            var vm = ViewModel;
            if (vm is null)
            {
                return;
            }

            try
            {
                var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
                {
                    Title = "打开 GameMaker 项目",
                    AllowMultiple = false,
                    FileTypeFilter = [GameMakerProjectFileType]
                });

                if (files.Count == 0)
                {
                    vm.AppendOutput("已取消打开项目。");
                    return;
                }

                var projectPath = TryGetLocalPath(files[0]);
                if (string.IsNullOrWhiteSpace(projectPath))
                {
                    vm.AppendOutput("无法读取所选项目路径。");
                    return;
                }

                var project = _projectSerializer.DeserializeProject(projectPath);
                vm.LoadProject(project, projectPath);
            }
            catch (Exception ex)
            {
                vm.AppendOutput($"打开项目失败：{ex.Message}");
            }
        }

        private async Task SaveProjectAsync()
        {
            var vm = ViewModel;
            if (vm is null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(vm.CurrentProjectFilePath))
            {
                await SaveProjectAsAsync();
                return;
            }

            try
            {
                var projectPath = NormalizeProjectFilePath(vm.CurrentProjectFilePath);
                var project = vm.EnsureCurrentProject();

                _projectSerializer.SerializeProject(projectPath, project);
                vm.MarkProjectSaved(projectPath, savedAs: false);
            }
            catch (Exception ex)
            {
                vm.AppendOutput($"保存项目失败：{ex.Message}");
            }
        }

        private async Task SaveProjectAsAsync()
        {
            var vm = ViewModel;
            if (vm is null)
            {
                return;
            }

            try
            {
                var suggestedFileName = GetSuggestedProjectFileName(vm);
                var storageFile = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
                {
                    Title = "另存为 GameMaker 项目",
                    SuggestedFileName = suggestedFileName,
                    ShowOverwritePrompt = true,
                    FileTypeChoices = [GameMakerProjectFileType]
                });

                if (storageFile is null)
                {
                    vm.AppendOutput("已取消另存为项目。");
                    return;
                }

                var projectPath = TryGetLocalPath(storageFile);
                if (string.IsNullOrWhiteSpace(projectPath))
                {
                    vm.AppendOutput("无法读取另存为目标路径。");
                    return;
                }

                projectPath = NormalizeProjectFilePath(projectPath);

                var project = vm.EnsureCurrentProject();
                _projectSerializer.SerializeProject(projectPath, project);
                vm.MarkProjectSaved(projectPath, savedAs: true);
            }
            catch (Exception ex)
            {
                vm.AppendOutput($"另存为项目失败：{ex.Message}");
            }
        }

        private static string GetSuggestedProjectFileName(MainWindowViewModel viewModel)
        {
            var baseName = string.IsNullOrWhiteSpace(viewModel.ProjectName)
                ? "Untitled"
                : viewModel.ProjectName;

            return baseName.EndsWith(".project.gmx", StringComparison.OrdinalIgnoreCase)
                ? baseName
                : $"{baseName}.project.gmx";
        }

        private static string NormalizeProjectFilePath(string path)
        {
            return path.EndsWith(".project.gmx", StringComparison.OrdinalIgnoreCase)
                ? path
                : $"{path}.project.gmx";
        }

        private static string? TryGetLocalPath(IStorageItem storageItem)
        {
            var uri = storageItem.Path;
            return uri.IsFile ? uri.LocalPath : null;
        }
    }
}
