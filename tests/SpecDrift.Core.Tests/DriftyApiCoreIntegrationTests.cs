using SpecDrift.Core.CodeReading;
using SpecDrift.Core.Comparing;
using SpecDrift.Core.Model;
using SpecDrift.Core.SpecReading;

namespace SpecDrift.Core.Tests;

/// <summary>
/// The ASP.NET Core fixture, run through the real pipeline end to end and asserted
/// against every drift documented in <c>samples/DriftyApi.Core/EXPECTED.md</c> — the
/// same ten drifts as <see cref="DriftyApiWebApi2IntegrationTests"/>, planted in Core
/// idiom instead of Web API 2's. This is what proves the dialect abstraction works
/// rather than merely existing: one walker, a second descriptor, the identical nine
/// deterministic findings. SD100 findings themselves need a real LLM call and are
/// out of scope here; the SemanticQuestion this comparer hands to that stage for
/// the delete endpoint is asserted directly.
/// </summary>
public class DriftyApiCoreIntegrationTests
{
    private readonly CodeReadResult _codeResult;
    private readonly IReadOnlyList<DriftFinding> _findings;
    private readonly IReadOnlyList<SemanticQuestion> _semanticQuestions;

    public DriftyApiCoreIntegrationTests()
    {
        using var specStream = File.OpenRead(RepoPath.Resolve("samples/openapi.yaml"));
        var specResult = SpecReader.Read(specStream);
        _codeResult = CodeReader.Read(RepoPath.Resolve("samples/DriftyApi.Core"), DialectDescriptors.Core);

        var comparison = Comparer.Compare(specResult, _codeResult, DialectChoice.Core);
        _findings = comparison.Findings;
        _semanticQuestions = comparison.SemanticQuestions;
    }

    [Fact]
    public void DeleteClientProducesTheSemanticQuestionSD100NeedsToCatch()
    {
        // SemanticQuestion.RouteTemplate comes from the spec side (the question is
        // keyed by the endpoint's identity, not either reader's own route syntax).
        var question = Assert.Single(_semanticQuestions, q => q.Method == HttpVerb.Delete && q.RouteTemplate == "/api/clients/{id}");

        Assert.Contains("cannot be undone", question.SpecDescription);
        Assert.Contains("Archived", question.CodeSourceText);
    }

    [Fact]
    public void ProducesExactlyTheNineDeterministicDriftsDocumentedInExpectedMd()
    {
        var codes = _findings.Select(f => f.Code).OrderBy(c => c, StringComparer.Ordinal).ToList();

        Assert.Equal(
            ["SD001", "SD002", "SD003", "SD004", "SD005", "SD006", "SD007", "SD008", "SD009"],
            codes);
    }

    [Fact]
    public void AllFindingsAreDeterministicAndTaggedCore()
    {
        Assert.All(_findings, f => Assert.Equal(Confidence.Certain, f.Confidence));
        Assert.All(_findings, f => Assert.Equal(DialectChoice.Core, f.Dialect));
    }

    [Fact]
    public void TheCodeReaderHasNoUndeterminableConstructs()
    {
        // Unlike the Web API 2 fixture's Problem()-wrapped-ResponseMessage idiom, every
        // return path in the Core controller resolves — Problem(statusCode: ...) names
        // its argument, and CreatedAtAction is a recognised fixed status helper. Nothing
        // here is a known blind spot.
        Assert.Empty(_codeResult.Limitations);
    }

    [Fact]
    public void SD001_MissingDocumentsEndpoint()
    {
        var finding = Single("SD001");

        Assert.Equal(Severity.High, finding.Severity);
        Assert.Null(finding.CodeCitation);
        var specCitation = Assert.IsType<SpecCitation>(finding.SpecCitation);
        Assert.Equal("#/paths/~1api~1clients~1{id}~1documents/get", specCitation.JsonPointer);
    }

    [Fact]
    public void SD002_UndocumentedAddDocumentAction()
    {
        var finding = Single("SD002");

        Assert.Equal(Severity.High, finding.Severity);
        Assert.Null(finding.SpecCitation);
        var codeCitation = Assert.IsType<CodeCitation>(finding.CodeCitation);
        Assert.Equal("Controllers/ClientsController.cs", codeCitation.RelativeFilePath);
        Assert.Contains("AddClientDocument", finding.Detail);
    }

    [Fact]
    public void SD003_SearchMethodMismatch()
    {
        var finding = Single("SD003");

        Assert.Equal(Severity.High, finding.Severity);
        Assert.NotNull(finding.SpecCitation);
        Assert.NotNull(finding.CodeCitation);
        Assert.Contains("POST", finding.Detail);
    }

    [Fact]
    public void SD004_UndocumentedRiskLevelParameter()
    {
        var finding = Single("SD004");

        Assert.Equal(Severity.Medium, finding.Severity);
        Assert.Contains("riskLevel", finding.Title);
    }

    [Fact]
    public void SD005_MissingKycSegmentParameter()
    {
        var finding = Single("SD005");

        Assert.Equal(Severity.Medium, finding.Severity);
        Assert.Contains("kycSegment", finding.Title);
    }

    [Fact]
    public void SD006_LeiRequirednessMismatch()
    {
        var finding = Single("SD006");

        Assert.Equal(Severity.Medium, finding.Severity);
        Assert.Contains("lei", finding.Title);
        Assert.Contains("optional", finding.Detail);
        Assert.Contains("required", finding.Detail);
    }

    [Fact]
    public void SD007_UndocumentedConflictStatusCode()
    {
        var finding = Single("SD007");

        Assert.Equal(Severity.Medium, finding.Severity);
        Assert.Contains("409", finding.Title);
    }

    [Fact]
    public void SD008_UndeliveredForbiddenStatusCode()
    {
        var finding = Single("SD008");

        Assert.Equal(Severity.Low, finding.Severity);
        Assert.Contains("403", finding.Title);
    }

    [Fact]
    public void SD009_SearchResultItemsTypeMismatch()
    {
        var finding = Single("SD009");

        Assert.Equal(Severity.Medium, finding.Severity);
        Assert.Contains("ClientSummary", finding.Detail);
        Assert.Contains("Client", finding.Detail);
    }

    private DriftFinding Single(string code) => Assert.Single(_findings, f => f.Code == code);
}
