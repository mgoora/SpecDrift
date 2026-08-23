namespace SpecDrift.Core.Tests;

/// <summary>Resolves a path relative to the repository root from a test's run directory.</summary>
internal static class RepoPath
{
    public static string Resolve(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "SpecDrift.sln")))
        {
            directory = directory.Parent;
        }

        if (directory is null)
        {
            throw new InvalidOperationException(
                "Could not locate the repository root (SpecDrift.sln) from the test base directory.");
        }

        return Path.Combine(directory.FullName, relativePath);
    }
}
