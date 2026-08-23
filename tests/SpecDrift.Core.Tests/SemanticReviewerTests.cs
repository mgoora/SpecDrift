using SpecDrift.Core.Model;
using SpecDrift.Core.SemanticReview;

namespace SpecDrift.Core.Tests;

public class SemanticReviewerTests
{
    private static SemanticQuestion Question(int index) => new(
        index,
        "/api/clients/{id}",
        HttpVerb.Delete,
        "Permanently removes the client record. This action cannot be undone.",
        "public IActionResult DeleteClient(Guid id) { client.KycStatus = KycStatus.Archived; return NoContent(); }",
        new SpecCitation("#/paths/~1api~1clients~1{id}/delete/description"),
        new CodeCitation("Controllers/ClientsController.cs", 100));

    [Fact]
    public async Task AContradictsVerdictBecomesASD100FindingThatIsAlwaysProbable()
    {
        var question = Question(0);
        var client = new FakeLlmClient([
            new SemanticVerdict(0, SemanticVerdictKind.Contradicts, "The handler archives rather than deletes.")
        ]);

        var findings = await SemanticReviewer.ReviewAsync([question], client, DialectChoice.WebApi2);

        var finding = Assert.Single(findings);
        Assert.Equal("SD100", finding.Code);
        Assert.Equal(Severity.Low, finding.Severity);
        Assert.Equal(Confidence.Probable, finding.Confidence);
        Assert.Equal(question.SpecCitation, finding.SpecCitation);
        Assert.Equal(question.CodeCitation, finding.CodeCitation);
        Assert.Equal("The handler archives rather than deletes.", finding.Detail);
    }

    [Theory]
    [InlineData(SemanticVerdictKind.Consistent)]
    [InlineData(SemanticVerdictKind.Unknown)]
    public async Task AConsistentOrUnknownVerdictProducesNoFinding(SemanticVerdictKind kind)
    {
        var client = new FakeLlmClient([new SemanticVerdict(0, kind, "Explanation.")]);

        var findings = await SemanticReviewer.ReviewAsync([Question(0)], client, DialectChoice.WebApi2);

        Assert.Empty(findings);
    }

    [Fact]
    public async Task AVerdictForAnIndexNeverSentIsIgnoredRatherThanCrashing()
    {
        var client = new FakeLlmClient([new SemanticVerdict(99, SemanticVerdictKind.Contradicts, "?")]);

        var findings = await SemanticReviewer.ReviewAsync([Question(0)], client, DialectChoice.WebApi2);

        Assert.Empty(findings);
    }

    [Fact]
    public async Task NoQuestionsMeansTheClientIsNeverCalled()
    {
        var client = new FakeLlmClient([]);

        var findings = await SemanticReviewer.ReviewAsync([], client, DialectChoice.WebApi2);

        Assert.Empty(findings);
        Assert.Null(client.ReceivedQuestions);
    }
}
