namespace SpecDrift.Core.Model;

/// <summary>
/// A residue the comparer's structural matching genuinely cannot resolve — for SD100,
/// whether the spec's own description still describes what the handler does. Batched
/// with every other <see cref="SemanticQuestion"/> from one run into a single LLM call.
/// </summary>
public sealed record SemanticQuestion(
    int Index,
    string RouteTemplate,
    HttpVerb Method,
    string SpecDescription,
    string CodeSourceText,
    SourceCitation SpecCitation,
    SourceCitation CodeCitation);

/// <summary>Never label an LLM verdict Certain, however confident the model sounds —
/// <see cref="SemanticVerdictKind.Unknown"/> is what the model returns when it
/// genuinely can't tell, and is reported as such rather than resolved by guessing.</summary>
public enum SemanticVerdictKind
{
    Contradicts,
    Consistent,
    Unknown
}

/// <summary>One answer to a <see cref="SemanticQuestion"/>, correlated back to it by
/// <see cref="QuestionIndex"/> rather than by array position — the model's own
/// ordering of its response isn't assumed to match the request's.</summary>
public sealed record SemanticVerdict(int QuestionIndex, SemanticVerdictKind Kind, string Explanation);
