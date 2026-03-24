using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using AvaloniaGM.ViewModels;

namespace AvaloniaGM.Views.Controls
{
    public partial class BackgroundEditorView : UserControl
    {
        private static readonly FilePickerFileType ImageFileType = new("Image Files")
        {
            Patterns = ["*.png", "*.bmp", "*.jpg", "*.jpeg", "*.gif"]
        };

        public BackgroundEditorView()
        {
            InitializeComponent();
        }

        private BackgroundEditorViewModel? ViewModel => DataContext as BackgroundEditorViewModel;

        private async void ImportImageButton_OnClick(object? sender, RoutedEventArgs e)
        {
            var viewModel = ViewModel;
            if (viewModel is null)
            {
                return;
            }

            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel?.StorageProvider is null)
            {
                return;
            }

            var file = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Import Background Image",
                AllowMultiple = false,
                FileTypeFilter = [ImageFileType]
            });

            if (file.Count == 0)
            {
                return;
            }

            var localPath = TryGetLocalPath(file[0]);
            if (string.IsNullOrWhiteSpace(localPath))
            {
                return;
            }

            try
            {
                viewModel.SetImage(new Bitmap(localPath));
                e.Handled = true;
            }
            catch (Exception ex)
            {
                viewModel.NotifyImageImportFailed(ex.Message);
            }
        }

        private static string? TryGetLocalPath(IStorageItem storageItem)
        {
            var uri = storageItem.Path;
            return uri.IsFile ? uri.LocalPath : null;
        }
    }
}
