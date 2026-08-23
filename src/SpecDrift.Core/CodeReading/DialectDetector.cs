using System.Xml.Linq;
using SpecDrift.Core.Model;

namespace SpecDrift.Core.CodeReading;

/// <summary>
/// Infers a project's dialect from its <c>.csproj</c> file's shape — SDK-style
/// (<c>&lt;Project Sdk="..."&gt;</c>) means ASP.NET Core; classic (a bare
/// <c>&lt;Project ToolsVersion="..."&gt;</c>, no <c>Sdk</c> attribute) means Web
/// API 2. Runs only when <c>--dialect</c> wasn't given; a wrong inference must be
/// visible in the report header, never silent, and a failed one is a tool error
/// rather than a silent default.
/// </summary>
public static class DialectDetector
{
    public static DialectChoice Detect(string projectDirectory)
    {
        var projectFile = Directory.EnumerateFiles(projectDirectory, "*.csproj", SearchOption.AllDirectories)
            .Where(path => !IsBuildOutputPath(path))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();

        if (projectFile is null)
        {
            throw new DialectDetectionException(
                $"No .csproj file found under '{projectDirectory}' — the dialect could not be detected. " +
                "Pass --dialect WebApi2|Core explicitly.");
        }

        var document = XDocument.Load(projectFile);
        var isSdkStyle = document.Root?.Attribute("Sdk") is not null;

        return isSdkStyle ? DialectChoice.Core : DialectChoice.WebApi2;
    }

    private static bool IsBuildOutputPath(string path)
    {
        var segments = path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return segments.Contains("bin", StringComparer.OrdinalIgnoreCase)
            || segments.Contains("obj", StringComparer.OrdinalIgnoreCase);
    }
}
