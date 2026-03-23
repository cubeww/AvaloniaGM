using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using AvaloniaGM.ViewModels;

namespace AvaloniaGM.Views.Controls
{
    public partial class SpriteEditorView : UserControl
    {
        private static readonly FilePickerFileType ImageFileType = new("Image Files")
        {
            Patterns = ["*.png", "*.bmp", "*.jpg", "*.jpeg", "*.gif"]
        };

        public SpriteEditorView()
        {
            InitializeComponent();
        }

        private SpriteEditorViewModel? ViewModel => DataContext as SpriteEditorViewModel;

        private async void AddFramesButton_OnClick(object? sender, RoutedEventArgs e)
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

            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Add Sprite Frames",
                AllowMultiple = true,
                FileTypeFilter = [ImageFileType]
            });

            if (files.Count == 0)
            {
                return;
            }

            var bitmaps = new List<Bitmap>();

            foreach (var file in files)
            {
                var localPath = TryGetLocalPath(file);
                if (string.IsNullOrWhiteSpace(localPath))
                {
                    continue;
                }

                try
                {
                    bitmaps.Add(new Bitmap(localPath));
                }
                catch
                {
                    // Ignore unreadable image files and continue with the rest.
                }
            }

            viewModel.AddFrames(bitmaps);
            e.Handled = true;
        }

        private static string? TryGetLocalPath(IStorageItem storageItem)
        {
            var uri = storageItem.Path;
            return uri.IsFile ? uri.LocalPath : null;
        }
    }
}
