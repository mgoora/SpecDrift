using SpecDrift.Core.CodeReading;
using SpecDrift.Core.Comparing;
using SpecDrift.Core.Model;
using SpecDrift.Core.SpecReading;

namespace SpecDrift.Core.Tests;

/// <summary>
/// The Web API 2 fixture, run through the real pipeline end to end (spec reader →
/// code reader → comparer) and asserted against every drift documented in
/// <c>samples/DriftyApi/EXPECTED.md</c> — proving the tool against a genuine legacy
/// codebase, not just synthetic snippets. SD100 findings themselves are out of
/// scope here — that needs a real LLM call, which no test makes — but the
/// SemanticQuestion this comparer hands to that stage for the delete endpoint is
/// asserted directly.
/// </summary>
public class DriftyApiWebApi2IntegrationTests
{
    private readonly CodeReadResult _codeResult;
    private readonly IReadOnlyList<DriftFinding> _findings;
    private readonly IReadOnlyList<SemanticQuestion> _semanticQuestions;

    public DriftyApiWebApi2IntegrationTests()
    {
        using var specStream = File.OpenRead(RepoPath.Resolve("samples/openapi.yaml"));
        var specResult = SpecReader.Read(specStream);
        _codeResult = CodeReader.Read(RepoPath.Resolve("samples/DriftyApi"), DialectDescriptors.WebApi2);

        var comparison = Comparer.Compare(specResult, _codeResult, DialectChoice.WebApi2);
        _findings = comparison.Findings;
        _semanticQuestions = comparison.SemanticQuestions;
    }

    [Fact]
    public void DeleteClientProducesTheSemanticQuestionSD100NeedsToCatch()
    {
        // The spec's description promises permanence ("cannot be undone"); the
        // handler actually archives. Structural comparison has nothing left to say —
        // route, method, parameters, and status code all agree — so this is exactly
        // the residue SD100 exists for.
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
    public void AllFindingsAreDeterministic()
    {
        // Nothing in this run went through the (not-yet-built) LLM stage.
        Assert.All(_findings, f => Assert.Equal(Confidence.Certain, f.Confidence));
        Assert.All(_findings, f => Assert.Equal(DialectChoice.WebApi2, f.Dialect));
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
        Assert.NotNull(finding.SpecCitation); // the operation exists in spec — just not this parameter
        Assert.NotNull(finding.CodeCitation);
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

    [Fact]
    public void DeclaresCreateClientsSuccessPathAsAKnownLimitation()
    {
        // CreateClient's success path returns ResponseMessage(response), where
        // `response`'s HttpStatusCode.Created lives on an earlier, separate statement
        // bound to a local variable — a genuine blind spot the reader declares rather
        // than guesses at, exactly as PROMPT.md's "known undeterminable cases" expects.
        Assert.Contains(_codeResult.Limitations, l => l.Contains("CreateClient") && l.Contains("could not be determined"));
    }

    private DriftFinding Single(string code) => Assert.Single(_findings, f => f.Code == code);
}
