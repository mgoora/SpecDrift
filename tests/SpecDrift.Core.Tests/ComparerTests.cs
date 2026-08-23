using SpecDrift.Core.CodeReading;
using SpecDrift.Core.Comparing;
using SpecDrift.Core.Model;
using SpecDrift.Core.SpecReading;

namespace SpecDrift.Core.Tests;

public class ComparerTests
{
    private static readonly IReadOnlyDictionary<string, SchemaShape> NoShapes =
        new Dictionary<string, SchemaShape>();

    private static ApiEndpoint SpecEndpoint(
        string route, HttpVerb method, string? operationName = null,
        IReadOnlyList<ApiParameter>? parameters = null, IReadOnlyList<ApiResponse>? responses = null,
        string? specDescription = null) =>
        new(route, method, new SpecCitation($"#/paths/{route}/{method.ToString().ToLowerInvariant()}"),
            parameters ?? [], null, responses ?? [], operationName, SpecDescription: specDescription);

    private static ApiEndpoint CodeEndpoint(
        string route, HttpVerb method, string? operationName = null,
        IReadOnlyList<ApiParameter>? parameters = null, IReadOnlyList<ApiResponse>? responses = null,
        bool hasUnresolvedResponses = false, string? codeSourceText = null) =>
        new(route, method, new CodeCitation("Controllers/WidgetsController.cs", 10),
            parameters ?? [], null, responses ?? [], operationName ?? "SomeAction", hasUnresolvedResponses,
            CodeSourceText: codeSourceText);

    private static ComparisonResult CompareFull(
        ApiSurface spec, ApiSurface code,
        IReadOnlyDictionary<string, SchemaShape>? specSchemas = null,
        IReadOnlyDictionary<string, SchemaShape>? codeTypes = null) =>
        Comparer.Compare(
            new SpecReadResult(spec, "1.0.0", specSchemas ?? NoShapes),
            new CodeReadResult(code, codeTypes ?? NoShapes, []),
            DialectChoice.WebApi2);

    private static IReadOnlyList<DriftFinding> Compare(
        ApiSurface spec, ApiSurface code,
        IReadOnlyDictionary<string, SchemaShape>? specSchemas = null,
        IReadOnlyDictionary<string, SchemaShape>? codeTypes = null) =>
        CompareFull(spec, code, specSchemas, codeTypes).Findings;

    [Fact]
    public void EndpointOnlyInSpec_IsReportedAsSD001()
    {
        var spec = new ApiSurface([SpecEndpoint("/api/widgets/{id}", HttpVerb.Get)]);
        var code = new ApiSurface([]);

        var findings = Compare(spec, code);

        var finding = Assert.Single(findings);
        Assert.Equal("SD001", finding.Code);
        Assert.Equal(Severity.High, finding.Severity);
        Assert.Equal(Confidence.Certain, finding.Confidence);
        Assert.NotNull(finding.SpecCitation);
        Assert.Null(finding.CodeCitation);
    }

    [Fact]
    public void EndpointOnlyInCode_IsReportedAsSD002()
    {
        var spec = new ApiSurface([]);
        var code = new ApiSurface([CodeEndpoint("api/widgets/{id}", HttpVerb.Get)]);

        var findings = Compare(spec, code);

        var finding = Assert.Single(findings);
        Assert.Equal("SD002", finding.Code);
        Assert.Null(finding.SpecCitation);
        Assert.NotNull(finding.CodeCitation);
    }

    [Fact]
    public void SameRouteDifferentMethodWithMatchingOperationName_IsReportedAsSD003()
    {
        var spec = new ApiSurface([SpecEndpoint("/api/widgets/search", HttpVerb.Get, "searchWidgets")]);
        var code = new ApiSurface([CodeEndpoint("api/widgets/search", HttpVerb.Post, "SearchWidgets")]);

        var findings = Compare(spec, code);

        var finding = Assert.Single(findings);
        Assert.Equal("SD003", finding.Code);
        Assert.NotNull(finding.SpecCitation);
        Assert.NotNull(finding.CodeCitation);
    }

    [Fact]
    public void SameRouteDifferentMethodWithUnrelatedOperationNames_IsReportedAsSeparateSD001AndSD002()
    {
        var spec = new ApiSurface([SpecEndpoint("/api/widgets/{id}/documents", HttpVerb.Get, "listWidgetDocuments")]);
        var code = new ApiSurface([CodeEndpoint("api/widgets/{id:guid}/documents", HttpVerb.Post, "AddWidgetDocument")]);

        var findings = Compare(spec, code);

        Assert.Equal(2, findings.Count);
        Assert.Contains(findings, f => f.Code == "SD001");
        Assert.Contains(findings, f => f.Code == "SD002");
        Assert.DoesNotContain(findings, f => f.Code == "SD003");
    }

    [Fact]
    public void SameRouteAndMethodWithRouteConstraintDifferences_ProducesNoFinding()
    {
        var spec = new ApiSurface([SpecEndpoint("/api/widgets/{id}", HttpVerb.Get, "getWidget")]);
        var code = new ApiSurface([CodeEndpoint("api/widgets/{id:guid}", HttpVerb.Get, "GetWidget")]);

        Assert.Empty(Compare(spec, code));
    }

    [Fact]
    public void FindingsAreOrderedMostSevereFirst()
    {
        var spec = new ApiSurface([SpecEndpoint("/api/widgets/{id}", HttpVerb.Get)]);
        var code = new ApiSurface([CodeEndpoint("api/other", HttpVerb.Post)]);

        var findings = Compare(spec, code);

        Assert.Equal(2, findings.Count);
        Assert.True(findings[0].Severity >= findings[^1].Severity);
    }

    [Fact]
    public void ParameterOnlyInCode_IsReportedAsSD004()
    {
        var codeParam = new ApiParameter("riskLevel", ParameterLocation.Query, false, "RiskLevel",
            new CodeCitation("Controllers/WidgetsController.cs", 42));
        var spec = new ApiSurface([SpecEndpoint("/api/widgets/search", HttpVerb.Get, "search")]);
        var code = new ApiSurface([CodeEndpoint("api/widgets/search", HttpVerb.Get, "search", [codeParam])]);

        var finding = Assert.Single(Compare(spec, code));

        Assert.Equal("SD004", finding.Code);
        Assert.Equal(Severity.Medium, finding.Severity);
        Assert.Equal(codeParam.Citation, finding.CodeCitation);
    }

    [Fact]
    public void ParameterOnlyInSpec_IsReportedAsSD005()
    {
        var specParam = new ApiParameter("kycSegment", ParameterLocation.Query, false, "KycSegment",
            new SpecCitation("#/paths/.../parameters/2"));
        var spec = new ApiSurface([SpecEndpoint("/api/widgets/search", HttpVerb.Get, "search", [specParam])]);
        var code = new ApiSurface([CodeEndpoint("api/widgets/search", HttpVerb.Get, "search")]);

        var finding = Assert.Single(Compare(spec, code));

        Assert.Equal("SD005", finding.Code);
        Assert.Equal(Severity.Medium, finding.Severity);
        Assert.Equal(specParam.Citation, finding.SpecCitation);
    }

    [Fact]
    public void ParameterRequirednessDisagreement_IsReportedAsSD006()
    {
        var specParam = new ApiParameter("lei", ParameterLocation.Query, false, "string", new SpecCitation("#/.../parameters/1"));
        var codeParam = new ApiParameter("lei", ParameterLocation.Query, true, "string", new CodeCitation("Controllers/WidgetsController.cs", 55));
        var spec = new ApiSurface([SpecEndpoint("/api/widgets/search", HttpVerb.Get, "search", [specParam])]);
        var code = new ApiSurface([CodeEndpoint("api/widgets/search", HttpVerb.Get, "search", [codeParam])]);

        var finding = Assert.Single(Compare(spec, code));

        Assert.Equal("SD006", finding.Code);
        Assert.Contains("optional", finding.Detail);
        Assert.Contains("required", finding.Detail);
    }

    [Fact]
    public void MatchingParameters_ProduceNoFinding()
    {
        var specParam = new ApiParameter("id", ParameterLocation.Path, true, "string", new SpecCitation("#/.../parameters/0"));
        var codeParam = new ApiParameter("id", ParameterLocation.Path, true, "Guid", new CodeCitation("Controllers/WidgetsController.cs", 20));
        var spec = new ApiSurface([SpecEndpoint("/api/widgets/{id}", HttpVerb.Get, "get", [specParam])]);
        var code = new ApiSurface([CodeEndpoint("api/widgets/{id}", HttpVerb.Get, "get", [codeParam])]);

        Assert.Empty(Compare(spec, code));
    }

    [Fact]
    public void StatusCodeReturnedButUndeclared_IsReportedAsSD007()
    {
        var codeResponse = new ApiResponse(409, null, new CodeCitation("Controllers/WidgetsController.cs", 84));
        var spec = new ApiSurface([SpecEndpoint("/api/widgets", HttpVerb.Post, "create",
            responses: [new ApiResponse(201, "Widget", new SpecCitation("#/.../responses/201"))])]);
        var code = new ApiSurface([CodeEndpoint("api/widgets", HttpVerb.Post, "create", responses:
        [
            codeResponse,
            new ApiResponse(201, null, new CodeCitation("Controllers/WidgetsController.cs", 90))
        ])]);

        var finding = Assert.Single(Compare(spec, code));

        Assert.Equal("SD007", finding.Code);
        Assert.Equal(Severity.Medium, finding.Severity);
        Assert.Equal(codeResponse.Citation, finding.CodeCitation);
    }

    [Fact]
    public void StatusCodeDeclaredButNeverReturned_IsReportedAsSD008_WhenTheEndpointIsFullyResolved()
    {
        var specResponse = new ApiResponse(403, null, new SpecCitation("#/.../responses/403"));
        var spec = new ApiSurface([SpecEndpoint("/api/widgets/{id}", HttpVerb.Delete, "delete",
            responses: [specResponse, new ApiResponse(204, null, new SpecCitation("#/.../responses/204"))])]);
        var code = new ApiSurface([CodeEndpoint("api/widgets/{id}", HttpVerb.Delete, "delete",
            responses: [new ApiResponse(204, null, new CodeCitation("Controllers/WidgetsController.cs", 30))],
            hasUnresolvedResponses: false)]);

        var finding = Assert.Single(Compare(spec, code));

        Assert.Equal("SD008", finding.Code);
        Assert.Equal(Severity.Low, finding.Severity);
        Assert.Equal(specResponse.Citation, finding.SpecCitation);
    }

    [Fact]
    public void StatusCodeDeclaredButNeverReturned_IsSuppressed_WhenTheEndpointHasUnresolvedReturns()
    {
        // The action has at least one return path the reader couldn't resolve, so
        // "never returned" isn't a claim it can stand behind for any of this
        // endpoint's spec-declared codes.
        var spec = new ApiSurface([SpecEndpoint("/api/widgets", HttpVerb.Post, "create",
            responses: [new ApiResponse(201, "Widget", new SpecCitation("#/.../responses/201"))])]);
        var code = new ApiSurface([CodeEndpoint("api/widgets", HttpVerb.Post, "create",
            responses: [], hasUnresolvedResponses: true)]);

        Assert.Empty(Compare(spec, code));
    }

    [Fact]
    public void ResponseTypeNameDisagreement_IsReportedAsSD009()
    {
        var spec = new ApiSurface([SpecEndpoint("/api/widgets/{id}", HttpVerb.Get, "get",
            responses: [new ApiResponse(200, "Widget", new SpecCitation("#/.../responses/200"))])]);
        var code = new ApiSurface([CodeEndpoint("api/widgets/{id}", HttpVerb.Get, "get", responses:
        [
            new ApiResponse(null, "WidgetEnvelope", new CodeCitation("Controllers/WidgetsController.cs", 15)),
            new ApiResponse(200, null, new CodeCitation("Controllers/WidgetsController.cs", 20))
        ])]);

        var finding = Assert.Single(Compare(spec, code));

        Assert.Equal("SD009", finding.Code);
        Assert.Contains("WidgetEnvelope", finding.Title);
    }

    [Fact]
    public void ResponseTypeSameNameButDifferingPropertyShape_IsReportedAsSD009()
    {
        // The type names agree on both sides ("SearchResult") — the drift is a
        // property underneath it (its list's element type) quietly changing, which a
        // top-level name comparison alone would miss entirely.
        var specSchema = new SchemaShape("SearchResult", new Dictionary<string, SchemaProperty>(StringComparer.OrdinalIgnoreCase)
        {
            ["items"] = new("array<WidgetSummary>", true, new SpecCitation("#/components/schemas/SearchResult/properties/items"))
        });
        var codeSchema = new SchemaShape("SearchResult", new Dictionary<string, SchemaProperty>(StringComparer.OrdinalIgnoreCase)
        {
            ["Items"] = new("array<Widget>", true, new CodeCitation("Models/SearchResult.cs", 5))
        });

        var spec = new ApiSurface([SpecEndpoint("/api/widgets/search", HttpVerb.Get, "search",
            responses: [new ApiResponse(200, "SearchResult", new SpecCitation("#/.../responses/200"))])]);
        var code = new ApiSurface([CodeEndpoint("api/widgets/search", HttpVerb.Get, "search", responses:
        [
            new ApiResponse(null, "SearchResult", new CodeCitation("Controllers/WidgetsController.cs", 12)),
            new ApiResponse(200, null, new CodeCitation("Controllers/WidgetsController.cs", 20))
        ])]);

        var finding = Assert.Single(Compare(
            spec, code,
            specSchemas: new Dictionary<string, SchemaShape> { ["SearchResult"] = specSchema },
            codeTypes: new Dictionary<string, SchemaShape> { ["SearchResult"] = codeSchema }));

        Assert.Equal("SD009", finding.Code);
        Assert.Contains("items", finding.Title);
        Assert.Contains("array<WidgetSummary>", finding.Detail);
        Assert.Contains("array<Widget>", finding.Detail);
    }

    [Fact]
    public void ResponseTypePrimitiveProperty_IsNotCompared_EvenWhenItsDescriptionDiffers()
    {
        // A plain OpenAPI primitive (string/format:uuid on the spec side) has no
        // equivalent-CLR-type table behind it — comparing its bare type description
        // against C#'s Guid would be a false positive on nearly every id/date property
        // in a real schema, not a genuine finding. IsSchemaReference: false is exactly
        // this fixture's shape: the spec described it as "string", the code as "Guid".
        var specShape = new SchemaShape("Widget", new Dictionary<string, SchemaProperty>(StringComparer.OrdinalIgnoreCase)
        {
            ["id"] = new("string", false, new SpecCitation("#/components/schemas/Widget/properties/id"))
        });
        var codeShape = new SchemaShape("Widget", new Dictionary<string, SchemaProperty>(StringComparer.OrdinalIgnoreCase)
        {
            ["Id"] = new("Guid", false, new CodeCitation("Models/Widget.cs", 2))
        });

        var spec = new ApiSurface([SpecEndpoint("/api/widgets/{id}", HttpVerb.Get, "get",
            responses: [new ApiResponse(200, "Widget", new SpecCitation("#/.../responses/200"))])]);
        var code = new ApiSurface([CodeEndpoint("api/widgets/{id}", HttpVerb.Get, "get", responses:
        [
            new ApiResponse(null, "Widget", new CodeCitation("Controllers/WidgetsController.cs", 12)),
            new ApiResponse(200, null, new CodeCitation("Controllers/WidgetsController.cs", 20))
        ])]);

        Assert.Empty(Compare(
            spec, code,
            specSchemas: new Dictionary<string, SchemaShape> { ["Widget"] = specShape },
            codeTypes: new Dictionary<string, SchemaShape> { ["Widget"] = codeShape }));
    }

    [Fact]
    public void ResponseTypeSameNameAndShape_ProducesNoFinding()
    {
        // "name" is a plain string — not a schema reference, and so not compared at
        // all (see ResponseTypePrimitiveProperty_IsNotCompared_EvenWhenItsDescriptionDiffers).
        // "kind" is a reference (an enum) that agrees on both sides, exercising the
        // actual property-comparison path rather than just the skip path.
        var shape = new SchemaShape("Widget", new Dictionary<string, SchemaProperty>(StringComparer.OrdinalIgnoreCase)
        {
            ["name"] = new("string", false, new SpecCitation("#/components/schemas/Widget/properties/name")),
            ["kind"] = new("WidgetKind", true, new SpecCitation("#/components/schemas/Widget/properties/kind"))
        });
        var codeShape = new SchemaShape("Widget", new Dictionary<string, SchemaProperty>(StringComparer.OrdinalIgnoreCase)
        {
            ["Name"] = new("string", false, new CodeCitation("Models/Widget.cs", 3)),
            ["Kind"] = new("WidgetKind", true, new CodeCitation("Models/Widget.cs", 4))
        });

        var spec = new ApiSurface([SpecEndpoint("/api/widgets/{id}", HttpVerb.Get, "get",
            responses: [new ApiResponse(200, "Widget", new SpecCitation("#/.../responses/200"))])]);
        var code = new ApiSurface([CodeEndpoint("api/widgets/{id}", HttpVerb.Get, "get", responses:
        [
            new ApiResponse(null, "Widget", new CodeCitation("Controllers/WidgetsController.cs", 12)),
            new ApiResponse(200, null, new CodeCitation("Controllers/WidgetsController.cs", 20))
        ])]);

        Assert.Empty(Compare(
            spec, code,
            specSchemas: new Dictionary<string, SchemaShape> { ["Widget"] = shape },
            codeTypes: new Dictionary<string, SchemaShape> { ["Widget"] = codeShape }));
    }

    [Fact]
    public void AMatchedEndpointWithADescriptionAndCodeProducesASemanticQuestion()
    {
        var spec = new ApiSurface([SpecEndpoint("/api/widgets/{id}", HttpVerb.Delete, "deleteWidget",
            specDescription: "Permanently removes the widget. This action cannot be undone.")]);
        var code = new ApiSurface([CodeEndpoint("api/widgets/{id}", HttpVerb.Delete, "DeleteWidget",
            codeSourceText: "public IActionResult DeleteWidget(string id) => NoContent();")]);

        var question = Assert.Single(CompareFull(spec, code).SemanticQuestions);

        Assert.Equal("/api/widgets/{id}", question.RouteTemplate);
        Assert.Equal(HttpVerb.Delete, question.Method);
        Assert.Contains("cannot be undone", question.SpecDescription);
        Assert.Contains("DeleteWidget", question.CodeSourceText);
    }

    [Fact]
    public void AMatchedEndpointWithNoSpecDescriptionProducesNoSemanticQuestion()
    {
        var spec = new ApiSurface([SpecEndpoint("/api/widgets/{id}", HttpVerb.Get, "getWidget")]);
        var code = new ApiSurface([CodeEndpoint("api/widgets/{id}", HttpVerb.Get, "GetWidget",
            codeSourceText: "public IActionResult GetWidget(string id) => Ok();")]);

        Assert.Empty(CompareFull(spec, code).SemanticQuestions);
    }

    [Fact]
    public void AnSD003PairedEndpointStillProducesASemanticQuestion()
    {
        var spec = new ApiSurface([SpecEndpoint("/api/widgets/search", HttpVerb.Get, "searchWidgets",
            specDescription: "Searches widgets.")]);
        var code = new ApiSurface([CodeEndpoint("api/widgets/search", HttpVerb.Post, "SearchWidgets",
            codeSourceText: "public IActionResult SearchWidgets() => Ok();")]);

        var result = CompareFull(spec, code);

        Assert.Contains(result.Findings, f => f.Code == "SD003");
        Assert.Single(result.SemanticQuestions);
    }

    [Fact]
    public void AnUnmatchedEndpointProducesNoSemanticQuestion()
    {
        var spec = new ApiSurface([SpecEndpoint("/api/widgets/{id}", HttpVerb.Get, "getWidget",
            specDescription: "Gets a widget.")]);
        var code = new ApiSurface([]);

        Assert.Empty(CompareFull(spec, code).SemanticQuestions);
    }
}
