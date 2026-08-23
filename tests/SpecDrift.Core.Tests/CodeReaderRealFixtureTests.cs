using SpecDrift.Core.CodeReading;
using SpecDrift.Core.Model;

namespace SpecDrift.Core.Tests;

/// <summary>
/// Reads the real <c>samples/DriftyApi</c> Web API 2 controller — a genuine legacy
/// fixture, not a synthetic one, proving the descriptor against one real case before
/// a second dialect is ever added.
/// </summary>
public class CodeReaderRealFixtureTests
{
    private readonly CodeReadResult _result;

    public CodeReaderRealFixtureTests()
    {
        _result = CodeReader.Read(RepoPath.Resolve("samples/DriftyApi"), DialectDescriptors.WebApi2);
    }

    [Fact]
    public void ReadsEveryActionOnTheController()
    {
        Assert.Equal(6, _result.Surface.Endpoints.Count);
    }

    [Fact]
    public void GetClient_HasAPathParameterAndFullyResolvedResponses()
    {
        var endpoint = Find(HttpVerb.Get, "api/clients/{id:guid}");

        var idParam = Assert.Single(endpoint.Parameters);
        Assert.Equal("id", idParam.Name);
        Assert.Equal(ParameterLocation.Path, idParam.Location);
        Assert.Equal("Guid", idParam.SchemaType);

        Assert.Null(endpoint.RequestBody);

        Assert.Contains(endpoint.Responses, r => r.StatusCode == null && r.SchemaName == "Client");
        Assert.Contains(endpoint.Responses, r => r.StatusCode == 200 && r.SchemaName == null);

        // The 404 path returns via a locally-defined Problem() helper whose first
        // argument is HttpStatusCode.NotFound — read directly from the source, so
        // this resolves too; nothing here is a genuine blind spot.
        Assert.Contains(endpoint.Responses, r => r.StatusCode == 404 && r.SchemaName == null);
        Assert.False(endpoint.HasUnresolvedResponses);
    }

    [Fact]
    public void CreateClient_ResolvesTheHelperCodesButNotTheSuccessPath()
    {
        var endpoint = Find(HttpVerb.Post, "api/clients");

        Assert.NotNull(endpoint.RequestBody);
        Assert.Equal("Client", endpoint.RequestBody!.SchemaName);
        Assert.True(endpoint.RequestBody.Required);

        // The three Problem(HttpStatusCode.X, ...) calls all resolve directly from
        // their own first argument. Only the success path — ResponseMessage(response),
        // where `response`'s HttpStatusCode.Created lives on an earlier, separate
        // statement bound to a local variable — stays a genuine blind spot.
        Assert.Contains(endpoint.Responses, r => r.StatusCode == 400);
        Assert.Contains(endpoint.Responses, r => r.StatusCode == 409);
        Assert.Contains(endpoint.Responses, r => r.StatusCode == null && r.SchemaName == "Client");
        Assert.True(endpoint.HasUnresolvedResponses);
    }

    [Fact]
    public void SearchClients_HasSixQueryParametersAndAResolvedSuccessCode()
    {
        var endpoint = Find(HttpVerb.Post, "api/clients/search");

        Assert.Equal(6, endpoint.Parameters.Count);
        Assert.All(endpoint.Parameters, p => Assert.Equal(ParameterLocation.Query, p.Location));

        // Every parameter is C#-optional (a default value), but `lei` is rejected at
        // runtime when absent via `if (string.IsNullOrWhiteSpace(lei)) return ...` —
        // that guard makes it effectively required despite its default.
        var lei = endpoint.Parameters.Single(p => p.Name == "lei");
        Assert.True(lei.Required);
        Assert.All(endpoint.Parameters.Where(p => p.Name != "lei"), p => Assert.False(p.Required));

        var riskLevel = endpoint.Parameters.Single(p => p.Name == "riskLevel");
        Assert.Equal("RiskLevel", riskLevel.SchemaType);

        Assert.Contains(endpoint.Responses, r => r.StatusCode == 200);
    }

    [Fact]
    public void DeleteClient_ResolvesBothItsStatusCodesAndHasNoResponseTypeAttribute()
    {
        var endpoint = Find(HttpVerb.Delete, "api/clients/{id:guid}");

        Assert.Equal(2, endpoint.Responses.Count);
        Assert.Contains(endpoint.Responses, r => r.StatusCode == 204);
        Assert.Contains(endpoint.Responses, r => r.StatusCode == 404);
        Assert.False(endpoint.HasUnresolvedResponses);
    }

    private ApiEndpoint Find(HttpVerb verb, string routeTemplate) =>
        _result.Surface.Endpoints.Single(e => e.Method == verb && e.RouteTemplate == routeTemplate);
}
