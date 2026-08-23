namespace SpecDrift.Core.Model;

/// <summary>
/// One reported drift. At least one of <see cref="SpecCitation"/> and
/// <see cref="CodeCitation"/> is always present — never both null, since a finding
/// with nothing to cite on either side is a bug, not a result. Existence findings
/// (SD001/SD002) cite only the side that has something; every other finding cites
/// both, since it only exists because the endpoint matched on both sides.
/// </summary>
public sealed record DriftFinding(
    string Code,
    string Title,
    Severity Severity,
    Confidence Confidence,
    DialectChoice Dialect,
    SourceCitation? SpecCitation,
    SourceCitation? CodeCitation,
    string Detail);
