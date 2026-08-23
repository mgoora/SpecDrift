using SpecDrift.Core.Model;
using SpecDrift.Core.Reporting;

namespace SpecDrift.Core.Tests;

public class ReportWriterTests
{
    [Fact]
    public void HeaderStatesDialectSpecVersionAndWhetherTheDialectWasDetected()
    {
        var detected = ReportWriter.Write(new ReportContext(DialectChoice.WebApi2, DialectSource.Detected, "1.0.0", [], []));
        var specified = ReportWriter.Write(new ReportContext(DialectChoice.Core, DialectSource.Specified, "1.0.0", [], []));

        Assert.Contains("**Dialect** WebApi2 (detected) · **Spec version** 1.0.0", detected);
        Assert.Contains("**Dialect** Core (--dialect) · **Spec version** 1.0.0", specified);
    }

    [Fact]
    public void SummaryCountsFindingsBySeverity()
    {
        var findings = new[]
        {
            Finding("SD001", Severity.High),
            Finding("SD002", Severity.High),
            Finding("SD004", Severity.Medium),
            Finding("SD008", Severity.Low),
        };

        var report = ReportWriter.Write(new ReportContext(DialectChoice.WebApi2, DialectSource.Detected, "1.0.0", findings, []));

        Assert.Contains("- High: 2", report);
        Assert.Contains("- Medium: 1", report);
        Assert.Contains("- Low: 1", report);
        Assert.Contains("- Total: 4", report);
    }

    [Fact]
    public void NoFindings_SaysSoRatherThanShowingAnEmptySection()
    {
        var report = ReportWriter.Write(new ReportContext(DialectChoice.WebApi2, DialectSource.Detected, "1.0.0", [], []));

        Assert.DoesNotContain("## Findings", report);
        Assert.Contains("No drift detected", report);
    }

    [Fact]
    public void AFindingWithOnlyASpecCitation_OmitsTheCodeLine()
    {
        var finding = new DriftFinding(
            "SD001", "Missing endpoint `GET /api/widgets`", Severity.High, Confidence.Certain,
            DialectChoice.WebApi2, new SpecCitation("#/paths/~1api~1widgets/get"), CodeCitation: null,
            "Detail text.");

        var report = ReportWriter.Write(new ReportContext(DialectChoice.WebApi2, DialectSource.Detected, "1.0.0", [finding], []));

        Assert.Contains("**Spec** `#/paths/~1api~1widgets/get`", report);
        Assert.DoesNotContain("**Code**", report);
    }

    [Fact]
    public void AFindingWithBothCitations_RendersBoth()
    {
        var finding = new DriftFinding(
            "SD003", "HTTP method mismatch on `/api/widgets/search`", Severity.High, Confidence.Certain,
            DialectChoice.WebApi2,
            new SpecCitation("#/paths/~1api~1widgets~1search/get"),
            new CodeCitation("Controllers/WidgetsController.cs", 42),
            "Detail text.");

        var report = ReportWriter.Write(new ReportContext(DialectChoice.WebApi2, DialectSource.Detected, "1.0.0", [finding], []));

        Assert.Contains("**Spec** `#/paths/~1api~1widgets~1search/get`", report);
        Assert.Contains("**Code** `Controllers/WidgetsController.cs:42`", report);
    }

    [Fact]
    public void LimitationsAreListedWhenPresentAndOmittedWhenNot()
    {
        var withLimitations = ReportWriter.Write(new ReportContext(
            DialectChoice.WebApi2, DialectSource.Detected, "1.0.0", [], ["Something could not be determined."]));
        var without = ReportWriter.Write(new ReportContext(DialectChoice.WebApi2, DialectSource.Detected, "1.0.0", [], []));

        Assert.Contains("## Limitations", withLimitations);
        Assert.Contains("- Something could not be determined.", withLimitations);
        Assert.DoesNotContain("## Limitations", without);
    }

    private static DriftFinding Finding(string code, Severity severity) => new(
        code, $"Title for {code}", severity, Confidence.Certain, DialectChoice.WebApi2,
        new SpecCitation($"#/paths/~1{code}/get"), null, "Detail.");
}
