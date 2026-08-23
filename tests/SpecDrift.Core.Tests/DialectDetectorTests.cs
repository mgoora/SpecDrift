using SpecDrift.Core.CodeReading;
using SpecDrift.Core.Model;

namespace SpecDrift.Core.Tests;

public class DialectDetectorTests : IDisposable
{
    private readonly string _projectDir;

    public DialectDetectorTests()
    {
        _projectDir = Path.Combine(Path.GetTempPath(), "specdrift-dialect-detector-tests-" + Guid.NewGuid());
        Directory.CreateDirectory(_projectDir);
    }

    public void Dispose() => Directory.Delete(_projectDir, recursive: true);

    [Fact]
    public void DetectsCoreFromTheRealFixture()
    {
        Assert.Equal(DialectChoice.Core, DialectDetector.Detect(RepoPath.Resolve("samples/DriftyApi.Core")));
    }

    [Fact]
    public void DetectsWebApi2FromTheRealFixture()
    {
        Assert.Equal(DialectChoice.WebApi2, DialectDetector.Detect(RepoPath.Resolve("samples/DriftyApi")));
    }

    [Fact]
    public void DetectsCoreFromAnSdkStyleProjectFile()
    {
        File.WriteAllText(Path.Combine(_projectDir, "Widgets.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk.Web">
              <PropertyGroup>
                <TargetFramework>net9.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);

        Assert.Equal(DialectChoice.Core, DialectDetector.Detect(_projectDir));
    }

    [Fact]
    public void DetectsWebApi2FromAClassicProjectFile()
    {
        File.WriteAllText(Path.Combine(_projectDir, "Widgets.csproj"), """
            <?xml version="1.0" encoding="utf-8"?>
            <Project ToolsVersion="15.0" DefaultTargets="Build" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
              <PropertyGroup>
                <OutputType>Library</OutputType>
              </PropertyGroup>
            </Project>
            """);

        Assert.Equal(DialectChoice.WebApi2, DialectDetector.Detect(_projectDir));
    }

    [Fact]
    public void ThrowsADetectionExceptionRatherThanGuessingWhenNoProjectFileExists()
    {
        Assert.Throws<DialectDetectionException>(() => DialectDetector.Detect(_projectDir));
    }
}
