using SpecDrift.Core.Model;

namespace SpecDrift.Cli.Tests;

public class CliOptionsParserTests : IDisposable
{
    private readonly string _specFile;
    private readonly string _projectDir;

    public CliOptionsParserTests()
    {
        _specFile = Path.GetTempFileName();
        _projectDir = Path.GetTempPath();
    }

    public void Dispose()
    {
        File.Delete(_specFile);
    }

    [Fact]
    public void MissingRequiredOptions_ReturnsExitCodeTwoAndNoOptions()
    {
        var result = CliOptionsParser.Parse(Array.Empty<string>(), TextWriter.Null);

        Assert.Null(result.Options);
        Assert.Equal(2, result.ExitCode);
    }

    [Theory]
    [InlineData("--help")]
    [InlineData("--version")]
    public void HelpAndVersion_ReturnExitCodeZeroAndNoOptions(string flag)
    {
        // These are the paths that hand control to System.CommandLine's own actions,
        // so they are the ones a change of library version is most likely to break.
        var result = CliOptionsParser.Parse([flag], TextWriter.Null);

        Assert.Null(result.Options);
        Assert.Equal(0, result.ExitCode);
    }

    [Fact]
    public void NonexistentSpecPath_ReturnsExitCodeTwo()
    {
        var missingPath = Path.Combine(_projectDir, Guid.NewGuid() + ".yaml");

        var result = CliOptionsParser.Parse(
            ["--spec", missingPath, "--project", _projectDir], TextWriter.Null);

        Assert.Null(result.Options);
        Assert.Equal(2, result.ExitCode);
    }

    [Fact]
    public void InvalidDialectValue_ReturnsExitCodeTwo()
    {
        var result = CliOptionsParser.Parse(
            ["--spec", _specFile, "--project", _projectDir, "--dialect", "Framework48"],
            TextWriter.Null);

        Assert.Null(result.Options);
        Assert.Equal(2, result.ExitCode);
    }

    [Fact]
    public void MinimalValidArguments_ProducesOptionsWithDefaults()
    {
        var result = CliOptionsParser.Parse(
            ["--spec", _specFile, "--project", _projectDir], TextWriter.Null);

        Assert.NotNull(result.Options);
        Assert.Equal(_specFile, result.Options.Spec.FullName);
        Assert.Equal(Path.TrimEndingDirectorySeparator(_projectDir),
            Path.TrimEndingDirectorySeparator(result.Options.Project.FullName));
        Assert.Null(result.Options.Dialect);
        Assert.Null(result.Options.Out);
        Assert.False(result.Options.NoLlm);
        Assert.Equal(Severity.Low, result.Options.MinSeverity);
    }

    [Fact]
    public void AllOptions_AreParsedIntoTheirTypedValues()
    {
        var outPath = Path.Combine(_projectDir, "report.md");

        var result = CliOptionsParser.Parse(
            [
                "--spec", _specFile,
                "--project", _projectDir,
                "--dialect", "Core",
                "--out", outPath,
                "--no-llm",
                "--min-severity", "High"
            ],
            TextWriter.Null);

        Assert.NotNull(result.Options);
        Assert.Equal(DialectChoice.Core, result.Options.Dialect);
        Assert.Equal(outPath, result.Options.Out?.FullName);
        Assert.True(result.Options.NoLlm);
        Assert.Equal(Severity.High, result.Options.MinSeverity);
    }
}
