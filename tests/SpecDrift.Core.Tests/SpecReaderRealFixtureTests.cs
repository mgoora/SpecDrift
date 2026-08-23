using SpecDrift.Core.Model;
using SpecDrift.Core.SpecReading;

namespace SpecDrift.Core.Tests;

/// <summary>
/// Reads the real <c>samples/openapi.yaml</c> shared by both dialect fixtures — the
/// same asserting-against-a-real-document approach the fixtures themselves use for
/// the comparer, applied one stage earlier.
/// </summary>
public class SpecReaderRealFixtureTests
{
    private readonly ApiSurface _surface;

    public SpecReaderRealFixtureTests()
    {
        using var stream = File.OpenRead(RepoPath.Resolve("samples/openapi.yaml"));
        _surface = SpecReader.Read(stream).Surface;
    }

    [Fact]
    public void ReadsEveryOperationInTheFixture()
    {
        Assert.Equal(6, _surface.Endpoints.Count);
    }

    [Fact]
    public void GetClientById_HasThePathParameterAndBothResponses()
    {
        var endpoint = Find(HttpVerb.Get, "/api/clients/{id}");

        var citation = Assert.IsType<SpecCitation>(endpoint.Citation);
        Assert.Equal("#/paths/~1api~1clients~1{id}/get", citation.JsonPointer);

        var idParam = Assert.Single(endpoint.Parameters);
        Assert.Equal("id", idParam.Name);
        Assert.Equal(ParameterLocation.Path, idParam.Location);
        Assert.True(idParam.Required);
        Assert.Equal("string", idParam.SchemaType);

        Assert.Null(endpoint.RequestBody);

        Assert.Equal(2, endpoint.Responses.Count);
        Assert.Contains(endpoint.Responses, r => r.StatusCode == 200 && r.SchemaName == "Client");
        Assert.Contains(endpoint.Responses, r => r.StatusCode == 404 && r.SchemaName == "ProblemDetails");
    }

    [Fact]
    public void CreateClient_HasARequiredRequestBodyAndCreatedResponse()
    {
        var endpoint = Find(HttpVerb.Post, "/api/clients");

        Assert.NotNull(endpoint.RequestBody);
        Assert.Equal("ClientCreate", endpoint.RequestBody!.SchemaName);
        Assert.True(endpoint.RequestBody.Required);

        Assert.Contains(endpoint.Responses, r => r.StatusCode == 201 && r.SchemaName == "Client");
        Assert.Contains(endpoint.Responses, r => r.StatusCode == 400 && r.SchemaName == "ProblemDetails");
    }

    [Fact]
    public void SearchClients_HasSixQueryParametersAndAnOptionalLei()
    {
        var endpoint = Find(HttpVerb.Get, "/api/clients/search");

        Assert.Equal(6, endpoint.Parameters.Count);
        Assert.All(endpoint.Parameters, p => Assert.Equal(ParameterLocation.Query, p.Location));

        var lei = endpoint.Parameters.Single(p => p.Name == "lei");
        Assert.False(lei.Required);

        var kycSegment = endpoint.Parameters.Single(p => p.Name == "kycSegment");
        Assert.Equal("KycSegment", kycSegment.SchemaType);

        Assert.Contains(endpoint.Responses, r => r.StatusCode == 200 && r.SchemaName == "ClientSearchResult");
    }

    [Fact]
    public void ListClientDocuments_DescribesAnArrayResponseByItsItemSchema()
    {
        var endpoint = Find(HttpVerb.Get, "/api/clients/{id}/documents");

        Assert.Contains(endpoint.Responses, r => r.StatusCode == 200 && r.SchemaName == "array<Document>");
    }

    [Fact]
    public void DeleteClient_HasANoContentResponseWithNoSchemaPlusForbiddenAndNotFound()
    {
        var endpoint = Find(HttpVerb.Delete, "/api/clients/{id}");

        var noContent = endpoint.Responses.Single(r => r.StatusCode == 204);
        Assert.Null(noContent.SchemaName);

        Assert.Contains(endpoint.Responses, r => r.StatusCode == 403);
        Assert.Contains(endpoint.Responses, r => r.StatusCode == 404);
    }

    private ApiEndpoint Find(HttpVerb verb, string routeTemplate) =>
        _surface.Endpoints.Single(e => e.Method == verb && e.RouteTemplate == routeTemplate);
}
