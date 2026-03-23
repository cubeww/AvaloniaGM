using System.Collections.Generic;

namespace AvaloniaGM.Models;

public class DataFile : Resource
{
    public string FileName { get; set; } = string.Empty;

    public string OriginalName { get; set; } = string.Empty;

    public bool Exists { get; set; } = true;

    public int Size { get; set; }

    public bool Store { get; set; }

    public byte[]? RawData { get; set; }

    public int ExportAction { get; set; } = 2;

    public string ExportDir { get; set; } = string.Empty;

    public bool Overwrite { get; set; }

    public bool FreeData { get; set; } = true;

    public bool RemoveEnd { get; set; }

    public Dictionary<string, long> ConfigOptions { get; } = [];

    public DataFile()
    {
        ConfigOptions["Default"] = long.MaxValue;
    }
}
