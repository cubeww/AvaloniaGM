using Avalonia.Media;
using Avalonia.Media.Imaging;
using AvaloniaGM.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AvaloniaGM.ViewModels;

public partial class FontEditorViewModel : ObservableObject
{
    private readonly Font _font;

    [ObservableProperty]
    private string fontName;

    [ObservableProperty]
    private float size;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PreviewFontWeight))]
    private bool bold;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PreviewFontStyle))]
    private bool italic;

    [ObservableProperty]
    private int first;

    [ObservableProperty]
    private int last;

    [ObservableProperty]
    private int charSet;

    [ObservableProperty]
    private int antiAlias;

    [ObservableProperty]
    private string previewText = "The quick brown fox jumps over the lazy dog 0123456789";

    public string Name => _font.Name;

    public int GlyphCount => _font.Glyphs.Count;

    public string RangeText => $"{First} - {Last}";

    public Bitmap? BitmapPreview => _font.Bitmap;

    public bool HasBitmapPreview => BitmapPreview is not null;

    public bool HasNoBitmapPreview => BitmapPreview is null;

    public string BitmapSizeText => BitmapPreview is null
        ? "No font atlas image"
        : $"{BitmapPreview.PixelSize.Width} x {BitmapPreview.PixelSize.Height}";

    public FontFamily PreviewFontFamily => new(FontName);

    public FontWeight PreviewFontWeight => Bold ? FontWeight.Bold : FontWeight.Normal;

    public FontStyle PreviewFontStyle => Italic ? FontStyle.Italic : FontStyle.Normal;

    public FontEditorViewModel(Font font)
    {
        _font = font;
        fontName = font.FontName;
        size = font.Size;
        bold = font.Bold;
        italic = font.Italic;
        first = font.First;
        last = font.Last;
        charSet = font.CharSet;
        antiAlias = font.AntiAlias;
    }

    partial void OnFontNameChanged(string value)
    {
        _font.FontName = value;
        OnPropertyChanged(nameof(PreviewFontFamily));
    }

    partial void OnSizeChanged(float value) => _font.Size = value;

    partial void OnBoldChanged(bool value) => _font.Bold = value;

    partial void OnItalicChanged(bool value) => _font.Italic = value;

    partial void OnFirstChanged(int value)
    {
        _font.First = value;
        SynchronizeSingleRange();
        OnPropertyChanged(nameof(RangeText));
    }

    partial void OnLastChanged(int value)
    {
        _font.Last = value;
        SynchronizeSingleRange();
        OnPropertyChanged(nameof(RangeText));
    }

    partial void OnCharSetChanged(int value) => _font.CharSet = value;

    partial void OnAntiAliasChanged(int value) => _font.AntiAlias = value;

    private void SynchronizeSingleRange()
    {
        if (_font.Ranges.Count == 0)
        {
            _font.Ranges.Add(new FontRange { Start = _font.First, End = _font.Last });
            return;
        }

        if (_font.Ranges.Count == 1)
        {
            _font.Ranges[0].Start = _font.First;
            _font.Ranges[0].End = _font.Last;
        }
    }
}
