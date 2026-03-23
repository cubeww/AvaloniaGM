namespace AvaloniaGM.Models;

public class Sound : Resource
{
    public int Kind { get; set; }

    public string Extension { get; set; } = string.Empty;

    public string OriginalName { get; set; } = string.Empty;

    public byte[]? RawData { get; set; }

    public int Effects { get; set; }

    public double Volume { get; set; }

    public double Pan { get; set; }

    public bool Preload { get; set; }

    public bool Compressed { get; set; }

    public bool Streamed { get; set; }

    public bool UncompressOnLoad { get; set; }

    public int CompressionQuality { get; set; } = 4;

    public int SampleRate { get; set; } = 44100;

    public bool Stereo { get; set; }

    public int BitDepth { get; set; } = 8;

    public int AudioGroup { get; set; }

    public string ExportDirectory { get; set; } = string.Empty;
}
