using System.Security.Cryptography;
using Avalonia;
using AvaloniaGM.Services;

AppBuilder.Configure<AvaloniaGM.App>()
    .UsePlatformDetect()
    .WithInterFont()
    .SetupWithoutStarting();

var workspaceRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
var sourceProjectPath = args.Length > 0
    ? Path.GetFullPath(args[0])
    : Path.Combine(workspaceRoot, "TestProjects", "jtool", "source.project.gmx");

var sourceProjectDirectory = Path.GetDirectoryName(sourceProjectPath)
    ?? throw new InvalidOperationException("Unable to resolve source project directory.");
var sourceProjectName = Path.GetFileNameWithoutExtension(sourceProjectPath);
var sourceProjectFolderName = new DirectoryInfo(sourceProjectDirectory).Name;

var outputDirectory = Path.Combine(workspaceRoot, "VerifierOutputs", sourceProjectFolderName);
if (Directory.Exists(outputDirectory))
{
    Directory.Delete(outputDirectory, recursive: true);
}

Directory.CreateDirectory(outputDirectory);

var outputProjectPath = Path.Combine(outputDirectory, Path.GetFileName(sourceProjectPath));

var serializer = new ProjectGmxSerializer();
var project = serializer.DeserializeProject(sourceProjectPath);
serializer.SerializeProject(outputProjectPath, project);

var comparison = CompareDirectories(sourceProjectDirectory, outputDirectory);

Console.WriteLine($"Source project : {sourceProjectPath}");
Console.WriteLine($"Output project : {outputProjectPath}");
Console.WriteLine($"Project name   : {project.Name}");
Console.WriteLine($"Resources      : sprites={project.Sprites.Count}, backgrounds={project.Backgrounds.Count}, sounds={project.Sounds.Count}, objects={project.Objects.Count}, rooms={project.Rooms.Count}, scripts={project.Scripts.Count}, datafiles={project.DataFiles.Count}, extensions={project.Extensions.Count}");
Console.WriteLine("Extensions     :");
foreach (var extension in project.Extensions.OrderBy(static extension => extension.Name, StringComparer.OrdinalIgnoreCase))
{
    Console.WriteLine($"- {extension.Name}: includes={extension.Includes.Count}, includedResources={extension.IncludedResources.Count}, packageFiles={extension.PackageFiles.Count}");
}
Console.WriteLine();
Console.WriteLine("Comparison summary");
Console.WriteLine($"- source files      : {comparison.SourceFileCount}");
Console.WriteLine($"- output files      : {comparison.OutputFileCount}");
Console.WriteLine($"- only in source    : {comparison.OnlyInSource.Count}");
Console.WriteLine($"- only in output    : {comparison.OnlyInOutput.Count}");
Console.WriteLine($"- changed common    : {comparison.Changed.Count}");
Console.WriteLine();

WriteSection("Only In Source", comparison.OnlyInSource);
WriteSection("Only In Output", comparison.OnlyInOutput);

var changedByExtension = comparison.Changed
    .GroupBy(static path => Path.GetExtension(path), StringComparer.OrdinalIgnoreCase)
    .OrderByDescending(static group => group.Count())
    .ThenBy(static group => group.Key, StringComparer.OrdinalIgnoreCase)
    .Select(static group => $"{(string.IsNullOrWhiteSpace(group.Key) ? "<no extension>" : group.Key)}: {group.Count()}")
    .ToList();
WriteSection("Changed By Extension", changedByExtension);

var importantChangedFiles = comparison.Changed
    .Where(static path =>
        path.EndsWith(".project.gmx", StringComparison.OrdinalIgnoreCase)
        || path.EndsWith(".gmx", StringComparison.OrdinalIgnoreCase)
        || path.EndsWith(".gml", StringComparison.OrdinalIgnoreCase)
        || path.EndsWith(".shader", StringComparison.OrdinalIgnoreCase))
    .Take(20)
    .ToList();
WriteSection("Representative Changed Text Files", importantChangedFiles);

Console.WriteLine("Done.");

return;

static DirectoryComparisonResult CompareDirectories(string sourceDirectory, string outputDirectory)
{
    var sourceFiles = Directory.GetFiles(sourceDirectory, "*", SearchOption.AllDirectories)
        .Select(path => NormalizeRelativePath(Path.GetRelativePath(sourceDirectory, path)))
        .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase)
        .ToList();
    var outputFiles = Directory.GetFiles(outputDirectory, "*", SearchOption.AllDirectories)
        .Select(path => NormalizeRelativePath(Path.GetRelativePath(outputDirectory, path)))
        .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase)
        .ToList();

    var sourceSet = new HashSet<string>(sourceFiles, StringComparer.OrdinalIgnoreCase);
    var outputSet = new HashSet<string>(outputFiles, StringComparer.OrdinalIgnoreCase);

    var onlyInSource = sourceFiles
        .Where(path => !outputSet.Contains(path))
        .ToList();
    var onlyInOutput = outputFiles
        .Where(path => !sourceSet.Contains(path))
        .ToList();

    var changed = new List<string>();
    foreach (var relativePath in sourceFiles.Where(outputSet.Contains))
    {
        var sourcePath = Path.Combine(sourceDirectory, relativePath);
        var outputPath = Path.Combine(outputDirectory, relativePath);

        if (!FilesEqual(sourcePath, outputPath))
        {
            changed.Add(relativePath);
        }
    }

    return new DirectoryComparisonResult(
        sourceFiles.Count,
        outputFiles.Count,
        onlyInSource,
        onlyInOutput,
        changed);
}

static bool FilesEqual(string leftPath, string rightPath)
{
    var leftInfo = new FileInfo(leftPath);
    var rightInfo = new FileInfo(rightPath);
    if (leftInfo.Length != rightInfo.Length)
    {
        return false;
    }

    using var leftStream = File.OpenRead(leftPath);
    using var rightStream = File.OpenRead(rightPath);
    var leftHash = SHA256.HashData(leftStream);
    var rightHash = SHA256.HashData(rightStream);

    return leftHash.AsSpan().SequenceEqual(rightHash);
}

static string NormalizeRelativePath(string path)
{
    return path.Replace(Path.DirectorySeparatorChar, '\\')
        .Replace(Path.AltDirectorySeparatorChar, '\\');
}

static void WriteSection(string title, IReadOnlyCollection<string> items)
{
    Console.WriteLine(title);
    if (items.Count == 0)
    {
        Console.WriteLine("- <none>");
        Console.WriteLine();
        return;
    }

    foreach (var item in items.Take(20))
    {
        Console.WriteLine($"- {item}");
    }

    if (items.Count > 20)
    {
        Console.WriteLine($"- ... ({items.Count - 20} more)");
    }

    Console.WriteLine();
}

internal sealed record DirectoryComparisonResult(
    int SourceFileCount,
    int OutputFileCount,
    List<string> OnlyInSource,
    List<string> OnlyInOutput,
    List<string> Changed);
