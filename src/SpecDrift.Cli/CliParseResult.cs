namespace SpecDrift.Cli;

/// <summary>
/// Either a validated <see cref="CliOptions"/> ready to drive the pipeline, or
/// an exit code the caller should return immediately — parse errors, <c>--help</c>,
/// and <c>--version</c> all resolve this way, without ever producing options.
/// </summary>
public sealed record CliParseResult(CliOptions? Options, int ExitCode);
