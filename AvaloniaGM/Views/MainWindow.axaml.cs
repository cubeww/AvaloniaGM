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
                    Title = "Open GameMaker Project",
                    AllowMultiple = false,
                    FileTypeFilter = [GameMakerProjectFileType]
                });

                if (files.Count == 0)
                {
                    vm.AppendOutput("Open project canceled.");
                    return;
                }

                var projectPath = TryGetLocalPath(files[0]);
                if (string.IsNullOrWhiteSpace(projectPath))
                {
                    vm.AppendOutput("Unable to read the selected project path.");
                    return;
                }

                OpenProjectFromPath(projectPath);
            }
            catch (Exception ex)
            {
                vm.AppendOutput($"Failed to open project: {ex.Message}");
            }
        }

        public void OpenProjectFromPath(string projectPath)
        {
            var vm = ViewModel;
            if (vm is null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(projectPath))
            {
                vm.AppendOutput("Unable to read the project path.");
                return;
            }

            var normalizedPath = Path.GetFullPath(projectPath);
            var project = _projectSerializer.DeserializeProject(normalizedPath);
            vm.LoadProject(project, normalizedPath);
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
                vm.AppendOutput($"Failed to save project: {ex.Message}");
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
                    Title = "Save GameMaker Project As",
                    SuggestedFileName = suggestedFileName,
                    ShowOverwritePrompt = true,
                    FileTypeChoices = [GameMakerProjectFileType]
                });

                if (storageFile is null)
                {
                    vm.AppendOutput("Save As canceled.");
                    return;
                }

                var projectPath = TryGetLocalPath(storageFile);
                if (string.IsNullOrWhiteSpace(projectPath))
                {
                    vm.AppendOutput("Unable to read the Save As target path.");
                    return;
                }

                projectPath = NormalizeProjectFilePath(projectPath);

                var project = vm.EnsureCurrentProject();
                _projectSerializer.SerializeProject(projectPath, project);
                vm.MarkProjectSaved(projectPath, savedAs: true);
            }
            catch (Exception ex)
            {
                vm.AppendOutput($"Failed to save project as: {ex.Message}");
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
