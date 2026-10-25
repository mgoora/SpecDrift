using SpecDrift.Core.Model;

namespace SpecDrift.Cli;

/// <summary>
/// The tool's parsed, validated invocation — the CLI contract in
/// <c>docs/ai-workflow/PROMPT.md</c> turned into data. <see cref="Dialect"/> null means
/// auto-detect; <see cref="Out"/> null means write the report to stdout.
/// </summary>
public sealed record CliOptions(
    FileInfo Spec,
    DirectoryInfo Project,
    DialectChoice? Dialect,
    FileInfo? Out,
    bool NoLlm,
    Severity MinSeverity);
