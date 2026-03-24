using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using AvaloniaGM.Models;

namespace AvaloniaGM.Services;

public class ProjectBuilder
{
    private readonly DataWinSerializer _dataWinSerializer;

    public ProjectBuilder()
        : this(new DataWinSerializer())
    {
    }

    public ProjectBuilder(DataWinSerializer dataWinSerializer)
    {
        _dataWinSerializer = dataWinSerializer ?? throw new ArgumentNullException(nameof(dataWinSerializer));
    }

    public void Build(Project project, string outputExePath)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputExePath);

        var fullExePath = Path.GetFullPath(outputExePath);
        var outputDirectory = Path.GetDirectoryName(fullExePath);
        if (string.IsNullOrWhiteSpace(outputDirectory))
        {
            throw new InvalidOperationException("Unable to resolve the output directory for the built executable.");
        }

        Directory.CreateDirectory(outputDirectory);

        _dataWinSerializer.SerializeProject(Path.Combine(outputDirectory, "data.win"), project);
        CopyDataFiles(project.DataFiles, outputDirectory);
        CopyExtensionFiles(project.Extensions, outputDirectory);
        ConvertStreamedSounds(project.Sounds, outputDirectory);

        CopyRunnerExecutable(fullExePath);
    }

    private static void CopyDataFiles(IEnumerable<DataFile> dataFiles, string outputDirectory)
    {
        foreach (var dataFile in dataFiles)
        {
            if (dataFile.RawData is null || string.IsNullOrWhiteSpace(dataFile.FileName))
            {
                continue;
            }

            WriteOutputFile(outputDirectory, GetDataFileOutputPath(dataFile.FileName), dataFile.RawData);
        }
    }

    private static void CopyExtensionFiles(IEnumerable<Extension> extensions, string outputDirectory)
    {
        foreach (var extension in extensions)
        {
            foreach (var includedResource in extension.IncludedResources)
            {
                if (includedResource.RawData is null || string.IsNullOrWhiteSpace(includedResource.FilePath))
                {
                    continue;
                }

                WriteOutputFile(
                    outputDirectory,
                    GetIncludedResourceOutputPath(extension, includedResource.FilePath),
                    includedResource.RawData);
            }

            foreach (var packageFile in extension.PackageFiles)
            {
                if (packageFile.RawData is null || string.IsNullOrWhiteSpace(packageFile.RelativePath))
                {
                    continue;
                }

                WriteOutputFile(outputDirectory, packageFile.RelativePath, packageFile.RawData);
            }

            foreach (var include in extension.Includes)
            {
                if (include.RawData is not null && !string.IsNullOrWhiteSpace(include.FileName))
                {
                    WriteOutputFile(outputDirectory, include.FileName, include.RawData);
                }

                foreach (var proxyFile in include.ProxyFiles)
                {
                    if (proxyFile.RawData is null || string.IsNullOrWhiteSpace(proxyFile.Name))
                    {
                        continue;
                    }

                    WriteOutputFile(outputDirectory, proxyFile.Name, proxyFile.RawData);
                }
            }
        }
    }

    private static string GetIncludedResourceOutputPath(Extension extension, string filePath)
    {
        var normalizedPath = NormalizeRelativePath(filePath);
        var extensionPrefix = NormalizeRelativePath(Path.Combine("extensions", extension.Name));
        if (normalizedPath.StartsWith(extensionPrefix + "/", StringComparison.OrdinalIgnoreCase))
        {
            return normalizedPath[(extensionPrefix.Length + 1)..];
        }

        return normalizedPath;
    }

    private static string GetDataFileOutputPath(string filePath)
    {
        var normalizedPath = NormalizeRelativePath(filePath);
        const string dataFilesPrefix = "datafiles/";
        return normalizedPath.StartsWith(dataFilesPrefix, StringComparison.OrdinalIgnoreCase)
            ? normalizedPath[dataFilesPrefix.Length..]
            : normalizedPath;
    }

    private static void CopyRunnerExecutable(string outputExePath)
    {
        var runnerPath = ResolveRunnerExecutablePath();
        Directory.CreateDirectory(Path.GetDirectoryName(outputExePath)!);
        File.Copy(runnerPath, outputExePath, overwrite: true);
    }

    private static string ResolveRunnerExecutablePath()
    {
        foreach (var candidate in EnumerateRunnerExecutablePaths())
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new FileNotFoundException("Runner.exe was not found. Expected it under a Runner folder beside the application output.");
    }

    private static void ConvertStreamedSounds(IEnumerable<Sound> sounds, string outputDirectory)
    {
        foreach (var sound in sounds)
        {
            if (!sound.Streamed)
            {
                continue;
            }

            ConvertStreamedSound(sound, outputDirectory);
        }
    }

    private static void ConvertStreamedSound(Sound sound, string outputDirectory)
    {
        if (sound.RawData is null || sound.RawData.Length == 0)
        {
            throw new InvalidOperationException($"Streamed sound '{sound.Name}' does not contain source audio data.");
        }

        var ffmpegPath = ResolveFfmpegExecutablePath();
        var outputPath = Path.Combine(outputDirectory, sound.Name + ".ogg");
        var tempDirectory = CreateTemporaryAudioDirectory(sound.Name);
        try
        {
            var inputPath = WriteTemporarySoundSource(sound, tempDirectory);
            if (IsOggFile(inputPath) && !RequiresOggReencode(sound))
            {
                File.Copy(inputPath, outputPath, overwrite: true);
                return;
            }

            ConvertSoundToOgg(sound, ffmpegPath, inputPath, outputPath);
        }
        finally
        {
            TryDeleteDirectory(tempDirectory);
        }
    }

    private static void ConvertSoundToOgg(Sound sound, string ffmpegPath, string inputPath, string outputPath)
    {
        var stereo = sound.Stereo;
        var sampleRate = Math.Max(1, sound.SampleRate);

        while (true)
        {
            var arguments = string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                "-y -i \"{0}\" -acodec libvorbis -bitexact -filter_complex \"aresample=ochl={2}c\" -ar {3} -aq {4} -loglevel quiet -vn \"{1}\"",
                inputPath,
                outputPath,
                stereo ? 2 : 1,
                sampleRate,
                Math.Clamp(sound.CompressionQuality, 0, 10));

            var exitCode = RunProcess(ffmpegPath, "-hide_banner -nostats -loglevel 0 " + arguments, Path.GetDirectoryName(ffmpegPath)!);
            if (exitCode == 0)
            {
                return;
            }

            if (!stereo)
            {
                stereo = true;
                continue;
            }

            if (sampleRate < 22050)
            {
                sampleRate *= 2;
                continue;
            }

            throw new InvalidOperationException($"Failed to convert streamed sound '{sound.Name}' to ogg.");
        }
    }

    private static bool RequiresOggReencode(Sound sound)
    {
        _ = sound;
        return false;
    }

    private static bool IsOggFile(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            Span<byte> header = stackalloc byte[4];
            if (stream.Read(header) != header.Length)
            {
                return false;
            }

            return header[0] == (byte)'O' &&
                   header[1] == (byte)'g' &&
                   header[2] == (byte)'g' &&
                   header[3] == (byte)'S';
        }
        catch
        {
            return false;
        }
    }

    private static string WriteTemporarySoundSource(Sound sound, string tempDirectory)
    {
        var extension = ResolveSoundSourceExtension(sound);
        var fileName = string.IsNullOrWhiteSpace(sound.Name) ? "sound" : sound.Name;
        var inputPath = Path.Combine(tempDirectory, fileName + extension);
        File.WriteAllBytes(inputPath, sound.RawData!);
        return inputPath;
    }

    private static string ResolveSoundSourceExtension(Sound sound)
    {
        var extension = !string.IsNullOrWhiteSpace(sound.Extension)
            ? sound.Extension
            : Path.GetExtension(sound.OriginalName ?? string.Empty);
        if (string.IsNullOrWhiteSpace(extension))
        {
            return ".wav";
        }

        return extension.StartsWith(".", StringComparison.Ordinal) ? extension : "." + extension;
    }

    private static string CreateTemporaryAudioDirectory(string soundName)
    {
        var directory = Path.Combine(Path.GetTempPath(), "AvaloniaGM", "BuildAudio", $"{SanitizeFileName(soundName)}_{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static string ResolveFfmpegExecutablePath()
    {
        foreach (var baseDirectory in EnumerateBaseDirectories())
        {
            foreach (var candidate in new[]
                     {
                         Path.Combine(baseDirectory, "FFmpeg", "ffmpeg.exe"),
                         Path.Combine(baseDirectory, "AvaloniaGM", "FFmpeg", "ffmpeg.exe"),
                     })
            {
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
        }

        throw new FileNotFoundException("ffmpeg.exe was not found. Expected it under an FFmpeg folder beside the application output.");
    }

    private static IEnumerable<string> EnumerateRunnerExecutablePaths()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var baseDirectory in EnumerateBaseDirectories())
        {
            foreach (var candidate in new[]
                     {
                         Path.Combine(baseDirectory, "Runner", "Runner.exe"),
                         Path.Combine(baseDirectory, "AvaloniaGM", "Runner", "Runner.exe"),
                     })
            {
                if (seen.Add(candidate))
                {
                    yield return candidate;
                }
            }
        }
    }

    private static IEnumerable<string> EnumerateBaseDirectories()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        for (var depth = 0; depth < 8 && current is not null; depth++, current = current.Parent)
        {
            yield return current.FullName;
        }

        var assemblyDirectory = Path.GetDirectoryName(typeof(ProjectBuilder).Assembly.Location);
        if (string.IsNullOrWhiteSpace(assemblyDirectory))
        {
            yield break;
        }

        current = new DirectoryInfo(assemblyDirectory);
        for (var depth = 0; depth < 8 && current is not null; depth++, current = current.Parent)
        {
            yield return current.FullName;
        }
    }

    private static void WriteOutputFile(string outputDirectory, string relativePath, byte[] data)
    {
        var sanitizedRelativePath = SanitizeRelativePath(relativePath);
        if (string.IsNullOrWhiteSpace(sanitizedRelativePath))
        {
            return;
        }

        var fullPath = Path.Combine(outputDirectory, sanitizedRelativePath);
        var fullDirectory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(fullDirectory))
        {
            Directory.CreateDirectory(fullDirectory);
        }

        File.WriteAllBytes(fullPath, data);
    }

    private static string NormalizeRelativePath(string path)
    {
        return path.Replace(Path.DirectorySeparatorChar, '/')
            .Replace(Path.AltDirectorySeparatorChar, '/')
            .TrimStart('/');
    }

    private static string SanitizeRelativePath(string path)
    {
        var normalized = NormalizeRelativePath(path);
        if (Path.IsPathRooted(normalized))
        {
            normalized = Path.GetFileName(normalized);
        }

        var parts = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var safeParts = new List<string>(parts.Length);
        foreach (var part in parts)
        {
            if (part is "." or "..")
            {
                continue;
            }

            safeParts.Add(part);
        }

        return string.Join(Path.DirectorySeparatorChar, safeParts);
    }

    private static int RunProcess(string executablePath, string arguments, string workingDirectory)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            Arguments = arguments,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Unable to start process '{executablePath}'.");
        process.StandardOutput.ReadToEnd();
        process.StandardError.ReadToEnd();
        process.WaitForExit();
        return process.ExitCode;
    }

    private static string SanitizeFileName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "file";
        }

        var invalidCharacters = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            builder.Append(Array.IndexOf(invalidCharacters, character) >= 0 ? '_' : character);
        }

        return builder.Length == 0 ? "file" : builder.ToString();
    }

    private static void TryDeleteDirectory(string path)
    {
        if (!Directory.Exists(path))
        {
            return;
        }

        try
        {
            Directory.Delete(path, recursive: true);
        }
        catch
        {
        }
    }
}
