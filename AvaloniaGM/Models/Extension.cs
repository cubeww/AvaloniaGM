using System;
using System.Collections.Generic;
using System.Globalization;

namespace AvaloniaGM.Models;

public class Extension : Resource
{
    public string Version { get; set; } = "1.0.0";

    public string Author { get; set; } = string.Empty;

    public string Date { get; set; } = DateTime.Today.ToString("dd/MM/yy", CultureInfo.InvariantCulture);

    public string License { get; set; } = "Free to use, also for commercial games.";

    public string Description { get; set; } = string.Empty;

    public string HelpFile { get; set; } = string.Empty;

    public string InstallDir { get; set; } = string.Empty;

    public string ClassName { get; set; } = string.Empty;

    public string AndroidClassName { get; set; } = string.Empty;

    public string MacSourceDir { get; set; } = string.Empty;

    public string MacLinkerFlags { get; set; } = string.Empty;

    public string MacCompilerFlags { get; set; } = string.Empty;

    public string ProductId { get; set; } = Guid.NewGuid().ToString("N").ToUpperInvariant();

    public string PackageId { get; set; } = string.Empty;

    public List<ExtensionFramework> IOSSystemFrameworks { get; } = [];

    public List<ExtensionFramework> IOSThirdPartyFrameworks { get; } = [];

    public Dictionary<string, long> ConfigOptions { get; } = [];

    public List<ExtensionIncludedResource> IncludedResources { get; } = [];

    public List<ExtensionPackageFile> PackageFiles { get; } = [];

    public List<ExtensionInclude> Includes { get; } = [];

    public Extension()
    {
        ConfigOptions["Default"] = 779092206;
    }
}

public class ExtensionIncludedResource
{
    public string FilePath { get; set; } = string.Empty;

    public byte[]? RawData { get; set; }
}

public class ExtensionPackageFile
{
    public string RelativePath { get; set; } = string.Empty;

    public byte[]? RawData { get; set; }
}

public class ExtensionInclude
{
    public string FileName { get; set; } = string.Empty;

    public string OriginalName { get; set; } = string.Empty;

    public byte[]? RawData { get; set; }

    public string Init { get; set; } = string.Empty;

    public string Final { get; set; } = string.Empty;

    public int Kind { get; set; }

    public bool Uncompress { get; set; }

    public Dictionary<string, long> ConfigOptions { get; } = [];

    public List<ExtensionProxyFile> ProxyFiles { get; } = [];

    public List<ExtensionFunction> Functions { get; } = [];

    public List<ExtensionConstant> Constants { get; } = [];
}

public class ExtensionFunction
{
    public string Name { get; set; } = string.Empty;

    public string ExternalName { get; set; } = string.Empty;

    public int Kind { get; set; }

    public string Help { get; set; } = string.Empty;

    public int ReturnType { get; set; }

    public int ArgCount { get; set; }

    public List<int> Args { get; } = [];
}

public class ExtensionConstant
{
    public string Name { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;
}

public class ExtensionFramework
{
    public string Name { get; set; } = string.Empty;

    public bool WeakReference { get; set; }
}

public class ExtensionProxyFile
{
    public string Name { get; set; } = string.Empty;

    public long TargetMask { get; set; }

    public byte[]? RawData { get; set; }
}
