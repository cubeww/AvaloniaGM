using System.Collections.Generic;

namespace AvaloniaGM.Models;

public class DataFile : Resource
{
    public string FileName { get; set; } = string.Empty;

    public string OriginalName { get; set; } = string.Empty;

    public bool Exists { get; set; }

    public int Size { get; set; }

    public bool Store { get; set; }

    public byte[]? RawData { get; set; }

    public int ExportAction { get; set; }

    public string ExportDir { get; set; } = string.Empty;

    public bool Overwrite { get; set; }

    public bool FreeData { get; set; }

    public bool RemoveEnd { get; set; }

    public Dictionary<string, long> ConfigOptions { get; } = [];
}
