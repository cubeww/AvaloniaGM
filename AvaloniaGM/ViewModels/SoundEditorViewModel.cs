using System;
using AvaloniaGM.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AvaloniaGM.ViewModels;

public partial class SoundEditorViewModel : ObservableObject
{
    private readonly Sound _sound;
    private readonly Action<string> _appendOutput;

    [ObservableProperty]
    private int kind;

    [ObservableProperty]
    private string extension;

    [ObservableProperty]
    private string originalName;

    [ObservableProperty]
    private int effects;

    [ObservableProperty]
    private double volume;

    [ObservableProperty]
    private double pan;

    [ObservableProperty]
    private bool preload;

    [ObservableProperty]
    private bool compressed;

    [ObservableProperty]
    private bool streamed;

    [ObservableProperty]
    private bool uncompressOnLoad;

    [ObservableProperty]
    private int compressionQuality;

    [ObservableProperty]
    private int sampleRate;

    [ObservableProperty]
    private bool stereo;

    [ObservableProperty]
    private int bitDepth;

    [ObservableProperty]
    private int audioGroup;

    [ObservableProperty]
    private string exportDirectory;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RemoveAudioCommand))]
    [NotifyPropertyChangedFor(nameof(HasAudio))]
    [NotifyPropertyChangedFor(nameof(HasNoAudio))]
    [NotifyPropertyChangedFor(nameof(AudioStatusText))]
    [NotifyPropertyChangedFor(nameof(AudioSizeText))]
    private byte[]? rawData;

    public string Name => _sound.Name;

    public bool HasAudio => RawData is { Length: > 0 };

    public bool HasNoAudio => !HasAudio;

    public string AudioStatusText => HasAudio
        ? $"Audio loaded ({AudioSizeText})"
        : "No audio file selected";

    public string AudioSizeText => RawData is { Length: > 0 }
        ? $"{RawData.Length} bytes"
        : "0 bytes";

    public SoundEditorViewModel(Sound sound, Action<string> appendOutput)
    {
        _sound = sound;
        _appendOutput = appendOutput;

        kind = sound.Kind;
        extension = sound.Extension;
        originalName = sound.OriginalName;
        effects = sound.Effects;
        volume = sound.Volume;
        pan = sound.Pan;
        preload = sound.Preload;
        compressed = sound.Compressed;
        streamed = sound.Streamed;
        uncompressOnLoad = sound.UncompressOnLoad;
        compressionQuality = sound.CompressionQuality;
        sampleRate = sound.SampleRate;
        stereo = sound.Stereo;
        bitDepth = sound.BitDepth;
        audioGroup = sound.AudioGroup;
        exportDirectory = sound.ExportDirectory;
        rawData = sound.RawData;
    }

    partial void OnKindChanged(int value) => _sound.Kind = value;

    partial void OnExtensionChanged(string value) => _sound.Extension = value;

    partial void OnOriginalNameChanged(string value) => _sound.OriginalName = value;

    partial void OnEffectsChanged(int value) => _sound.Effects = value;

    partial void OnVolumeChanged(double value) => _sound.Volume = value;

    partial void OnPanChanged(double value) => _sound.Pan = value;

    partial void OnPreloadChanged(bool value) => _sound.Preload = value;

    partial void OnCompressedChanged(bool value) => _sound.Compressed = value;

    partial void OnStreamedChanged(bool value) => _sound.Streamed = value;

    partial void OnUncompressOnLoadChanged(bool value) => _sound.UncompressOnLoad = value;

    partial void OnCompressionQualityChanged(int value) => _sound.CompressionQuality = value;

    partial void OnSampleRateChanged(int value) => _sound.SampleRate = value;

    partial void OnStereoChanged(bool value) => _sound.Stereo = value;

    partial void OnBitDepthChanged(int value) => _sound.BitDepth = value;

    partial void OnAudioGroupChanged(int value) => _sound.AudioGroup = value;

    partial void OnExportDirectoryChanged(string value) => _sound.ExportDirectory = value;

    public void SetAudio(byte[] rawData, string filePath)
    {
        ArgumentNullException.ThrowIfNull(rawData);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var fileName = System.IO.Path.GetFileName(filePath);
        var extensionValue = System.IO.Path.GetExtension(filePath);

        RawData = rawData;
        OriginalName = fileName;
        Extension = extensionValue;

        _sound.RawData = rawData;
        _sound.OriginalName = fileName;
        _sound.Extension = extensionValue;

        RefreshDerivedState();
        _appendOutput($"Imported audio for sound {Name}: {fileName} ({AudioSizeText}).");
    }

    public void NotifyAudioImportFailed(string details)
    {
        _appendOutput($"Failed to import audio for sound {Name}: {details}");
    }

    [RelayCommand(CanExecute = nameof(CanRemoveAudio))]
    private void RemoveAudio()
    {
        RawData = null;
        OriginalName = string.Empty;
        Extension = string.Empty;

        _sound.RawData = null;
        _sound.OriginalName = string.Empty;
        _sound.Extension = string.Empty;

        RefreshDerivedState();
        _appendOutput($"Removed audio from sound {Name}.");
    }

    private bool CanRemoveAudio() => HasAudio;

    private void RefreshDerivedState()
    {
        OnPropertyChanged(nameof(HasAudio));
        OnPropertyChanged(nameof(HasNoAudio));
        OnPropertyChanged(nameof(AudioStatusText));
        OnPropertyChanged(nameof(AudioSizeText));
    }
}
