using SpecDrift.Core.Model;

namespace SpecDrift.Core.SemanticReview;

/// <summary>
/// Stage 4 of the pipeline: sends every <see cref="SemanticQuestion"/> the comparer
/// collected to the LLM client in a single batch, and turns each "contradicts"
/// verdict into an SD100 finding. Every SD100 finding is <see cref="Confidence.Probable"/>
/// — never Certain, however confident the model sounds. A Consistent or Unknown
/// verdict produces no finding: "the model couldn't tell" is not itself a drift. Nor
/// does a contradiction the model marks as restating an already-reported finding:
/// the deterministic finding says it with certainty, so SD100 would only repeat it.
/// </summary>
public static class SemanticReviewer
{
    public static async Task<IReadOnlyList<DriftFinding>> ReviewAsync(
        IReadOnlyList<SemanticQuestion> questions,
        ILlmClient client,
        DialectChoice dialect,
        CancellationToken cancellationToken = default)
    {
        if (questions.Count == 0)
        {
            return [];
        }

        var verdicts = await client.ReviewAsync(questions, cancellationToken);
        var questionsByIndex = questions.ToDictionary(q => q.Index);

        var findings = new List<DriftFinding>();

        foreach (var verdict in verdicts)
        {
            if (verdict.Kind != SemanticVerdictKind.Contradicts || verdict.RestatesReported)
            {
                continue;
            }

            if (!questionsByIndex.TryGetValue(verdict.QuestionIndex, out var question))
            {
                continue; // the model echoed back an index we never sent — ignore it rather than guess
            }

            findings.Add(new DriftFinding(
                "SD100",
                $"Description may contradict behavior: {question.Method} {question.RouteTemplate}",
                Severity.Low,
                Confidence.Probable,
                dialect,
                question.SpecCitation,
                question.CodeCitation,
                verdict.Explanation));
        }

        return findings;
    }
}
