using SpecDrift.Core.Model;

namespace SpecDrift.Core.Comparing;

/// <summary>The comparer's output: deterministic findings, plus every residue —
/// SD100 candidates — that structural comparison genuinely can't resolve on its own.
/// <see cref="SemanticQuestions"/> is empty and untouched under <c>--no-llm</c>; the
/// caller decides whether to send it anywhere.</summary>
public sealed record ComparisonResult(
    IReadOnlyList<DriftFinding> Findings,
    IReadOnlyList<SemanticQuestion> SemanticQuestions);
