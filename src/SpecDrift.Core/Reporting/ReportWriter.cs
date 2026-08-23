using System.Text;
using SpecDrift.Core.Model;

namespace SpecDrift.Core.Reporting;

/// <summary>
/// Renders a <see cref="ReportContext"/> as the markdown report. The header always
/// states the dialect and spec version — a wrong dialect inference must be visible,
/// never silent — and every finding cites whichever of spec/code it has.
/// </summary>
public static class ReportWriter
{
    public static string Write(ReportContext context)
    {
        var writer = new StringBuilder();

        writer.AppendLine("# Spec Drift Report");
        writer.AppendLine();
        var dialectSourceLabel = context.DialectSource == DialectSource.Detected ? "detected" : "--dialect";
        writer.AppendLine($"**Dialect** {context.Dialect} ({dialectSourceLabel}) · **Spec version** {context.SpecVersion}");
        writer.AppendLine();

        WriteSummary(writer, context.Findings);

        if (context.Findings.Count == 0)
        {
            writer.AppendLine("No drift detected at or above the requested severity.");
            writer.AppendLine();
        }
        else
        {
            writer.AppendLine("## Findings");
            writer.AppendLine();

            foreach (var finding in context.Findings)
            {
                WriteFinding(writer, finding);
            }
        }

        if (context.Limitations.Count > 0)
        {
            writer.AppendLine("## Limitations");
            writer.AppendLine();

            foreach (var limitation in context.Limitations)
            {
                writer.AppendLine($"- {limitation}");
            }

            writer.AppendLine();
        }

        return writer.ToString();
    }

    private static void WriteSummary(StringBuilder writer, IReadOnlyList<DriftFinding> findings)
    {
        var high = findings.Count(f => f.Severity == Severity.High);
        var medium = findings.Count(f => f.Severity == Severity.Medium);
        var low = findings.Count(f => f.Severity == Severity.Low);

        writer.AppendLine("## Summary");
        writer.AppendLine();
        writer.AppendLine($"- High: {high}");
        writer.AppendLine($"- Medium: {medium}");
        writer.AppendLine($"- Low: {low}");
        writer.AppendLine($"- Total: {findings.Count}");
        writer.AppendLine();
    }

    private static void WriteFinding(StringBuilder writer, DriftFinding finding)
    {
        writer.AppendLine($"### {finding.Code} · {finding.Title}");
        writer.AppendLine($"**Severity** {finding.Severity} · **Confidence** {finding.Confidence} · **Dialect** {finding.Dialect}");

        if (finding.CodeCitation is not null)
        {
            writer.AppendLine($"**Code** `{finding.CodeCitation.Describe()}`");
        }

        if (finding.SpecCitation is not null)
        {
            writer.AppendLine($"**Spec** `{finding.SpecCitation.Describe()}`");
        }

        writer.AppendLine();
        writer.AppendLine(finding.Detail);
        writer.AppendLine();
        writer.AppendLine("---");
        writer.AppendLine();
    }
}
