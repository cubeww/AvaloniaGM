using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using AvaloniaGM.ViewModels;
using AvaloniaGM.Views;
using System;
using System.IO;
using System.Linq;

namespace AvaloniaGM
{
    public partial class App : Application
    {
        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                // Avoid duplicate validations from both Avalonia and the CommunityToolkit. 
                // More info: https://docs.avaloniaui.net/docs/guides/development-guides/data-validation#manage-validationplugins
                DisableAvaloniaDataAnnotationValidation();
                var mainWindow = new MainWindow
                {
                    DataContext = new MainWindowViewModel(),
                };

                desktop.MainWindow = mainWindow;
                OpenStartupProjectFromArguments(desktop.Args, mainWindow);
            }

            base.OnFrameworkInitializationCompleted();
        }

        private void DisableAvaloniaDataAnnotationValidation()
        {
            // Get an array of plugins to remove
            var dataValidationPluginsToRemove =
                BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

            // remove each entry found
            foreach (var plugin in dataValidationPluginsToRemove)
            {
                BindingPlugins.DataValidators.Remove(plugin);
            }
        }

        private static void OpenStartupProjectFromArguments(string[]? args, MainWindow mainWindow)
        {
            var startupProjectPath = GetStartupProjectPath(args);
            if (startupProjectPath is null)
            {
                return;
            }

            var viewModel = mainWindow.DataContext as MainWindowViewModel;
            if (viewModel is null)
            {
                return;
            }

            if (!File.Exists(startupProjectPath))
            {
                viewModel.AppendOutput($"Startup project not found: {startupProjectPath}");
                return;
            }

            try
            {
                mainWindow.OpenProjectFromPath(startupProjectPath);
            }
            catch (Exception ex)
            {
                viewModel.AppendOutput($"Failed to open startup project: {ex.Message}");
            }
        }

        private static string? GetStartupProjectPath(string[]? args)
        {
            if (args is null || args.Length == 0)
            {
                return null;
            }

            foreach (var arg in args)
            {
                if (!arg.EndsWith(".project.gmx", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                return Path.GetFullPath(arg);
            }

            return null;
        }
    }
}
