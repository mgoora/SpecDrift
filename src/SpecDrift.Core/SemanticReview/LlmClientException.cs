namespace SpecDrift.Core.SemanticReview;

/// <summary>The LLM call failed or returned something the client couldn't make sense
/// of. A tool error (exit code 2) — never silently treated as "no drift found".</summary>
public sealed class LlmClientException : Exception
{
    public LlmClientException(string message) : base(message)
    {
    }
}
