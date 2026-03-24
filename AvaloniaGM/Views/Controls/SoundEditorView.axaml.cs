using System;
using System.IO;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using AvaloniaGM.ViewModels;

namespace AvaloniaGM.Views.Controls
{
    public partial class SoundEditorView : UserControl
    {
        private static readonly FilePickerFileType AudioFileType = new("Audio Files")
        {
            Patterns = ["*.wav", "*.ogg", "*.mp3", "*.flac", "*.m4a", "*.aac"]
        };

        public SoundEditorView()
        {
            InitializeComponent();
        }

        private SoundEditorViewModel? ViewModel => DataContext as SoundEditorViewModel;

        private async void ImportAudioButton_OnClick(object? sender, RoutedEventArgs e)
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
                Title = "Import Audio File",
                AllowMultiple = false,
                FileTypeFilter = [AudioFileType]
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
                var rawData = await File.ReadAllBytesAsync(localPath);
                viewModel.SetAudio(rawData, localPath);
                e.Handled = true;
            }
            catch (Exception ex)
            {
                viewModel.NotifyAudioImportFailed(ex.Message);
            }
        }

        private static string? TryGetLocalPath(IStorageItem storageItem)
        {
            var uri = storageItem.Path;
            return uri.IsFile ? uri.LocalPath : null;
        }
    }
}
