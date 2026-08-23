using SpecDrift.Core.Model;
using SpecDrift.Core.SemanticReview;

namespace SpecDrift.Core.Tests;

/// <summary>The fake required by the testing rule "ILlmClient is faked in tests — no
/// test makes a network call." Records what it was asked and returns whatever
/// verdicts the test configured.</summary>
public sealed class FakeLlmClient : ILlmClient
{
    private readonly IReadOnlyList<SemanticVerdict> _verdicts;

    public FakeLlmClient(IReadOnlyList<SemanticVerdict> verdicts)
    {
        _verdicts = verdicts;
    }

    public IReadOnlyList<SemanticQuestion>? ReceivedQuestions { get; private set; }

    public Task<IReadOnlyList<SemanticVerdict>> ReviewAsync(
        IReadOnlyList<SemanticQuestion> questions, CancellationToken cancellationToken = default)
    {
        ReceivedQuestions = questions;
        return Task.FromResult(_verdicts);
    }
}
