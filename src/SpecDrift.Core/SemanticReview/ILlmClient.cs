using SpecDrift.Core.Model;

namespace SpecDrift.Core.SemanticReview;

/// <summary>
/// Stage 4 of the pipeline: takes the comparer's <see cref="SemanticQuestion"/> list —
/// batched into one call — and returns a verdict per question. The one seam in the
/// tool that leaves the process. Faked in every test; no test makes a network call.
/// Skipped entirely under <c>--no-llm</c>.
/// </summary>
public interface ILlmClient
{
    Task<IReadOnlyList<SemanticVerdict>> ReviewAsync(
        IReadOnlyList<SemanticQuestion> questions, CancellationToken cancellationToken = default);
}
