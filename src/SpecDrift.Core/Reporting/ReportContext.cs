using SpecDrift.Core.Model;

namespace SpecDrift.Core.Reporting;

/// <summary>Everything the report writer needs, already decided by the caller —
/// which findings to include (already filtered by <c>--min-severity</c>) and which
/// limitations to declare.</summary>
public sealed record ReportContext(
    DialectChoice Dialect,
    DialectSource DialectSource,
    string SpecVersion,
    IReadOnlyList<DriftFinding> Findings,
    IReadOnlyList<string> Limitations);
