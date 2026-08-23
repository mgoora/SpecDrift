using System.Net;
using System.Text;
using System.Text.Json;
using SpecDrift.Core.Model;
using SpecDrift.Core.SemanticReview;

namespace SpecDrift.Core.Tests;

/// <summary>
/// Verifies AnthropicLlmClient's request/response contract against a stubbed
/// HttpMessageHandler — never a real network call, per "no test makes a network call".
/// </summary>
public class AnthropicLlmClientTests
{
    private static SemanticQuestion Question(int index) => new(
        index,
        "/api/widgets/{id}",
        HttpVerb.Delete,
        "Permanently deletes the widget.",
        "public IActionResult Delete(string id) => NoContent();",
        new SpecCitation("#/paths/~1api~1widgets~1{id}/delete"),
        new CodeCitation("Controllers/WidgetsController.cs", 10));

    private static HttpResponseMessage JsonResponse(HttpStatusCode status, string json) => new(status)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json")
    };

    private static string TextResponseEnvelope(string verdictsJson) =>
        JsonSerializer.Serialize(new { content = new[] { new { type = "text", text = verdictsJson } } });

    [Fact]
    public async Task SendsOneRequestBundlingEveryQuestionWithTheExpectedModelAndSchema()
    {
        var responseJson = TextResponseEnvelope(
            """{"verdicts":[{"index":0,"verdict":"contradicts","explanation":"Archives, doesn't delete."}]}""");

        var handler = new StubHttpMessageHandler((request, body) =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("https://api.anthropic.com/v1/messages", request.RequestUri!.ToString());

            using var document = JsonDocument.Parse(body!);
            var root = document.RootElement;

            Assert.Equal("claude-haiku-4-5", root.GetProperty("model").GetString());
            Assert.True(root.GetProperty("max_tokens").GetInt32() > 0);
            Assert.Equal("json_schema", root.GetProperty("output_config").GetProperty("format").GetProperty("type").GetString());

            var userContent = root.GetProperty("messages")[0].GetProperty("content").GetString();
            Assert.Contains("Permanently deletes the widget.", userContent);
            Assert.Contains("public IActionResult Delete", userContent);

            return JsonResponse(HttpStatusCode.OK, responseJson);
        });

        using var httpClient = new HttpClient(handler);
        using var client = new AnthropicLlmClient(httpClient, ownsHttpClient: false);

        var verdicts = await client.ReviewAsync([Question(0)]);

        var verdict = Assert.Single(verdicts);
        Assert.Equal(0, verdict.QuestionIndex);
        Assert.Equal(SemanticVerdictKind.Contradicts, verdict.Kind);
        Assert.Equal("Archives, doesn't delete.", verdict.Explanation);
    }

    [Fact]
    public async Task AnUnrecognisedVerdictLabelBecomesUnknownRatherThanGuessed()
    {
        var responseJson = TextResponseEnvelope("""{"verdicts":[{"index":0,"verdict":"maybe","explanation":"?"}]}""");
        var handler = new StubHttpMessageHandler((_, _) => JsonResponse(HttpStatusCode.OK, responseJson));

        using var httpClient = new HttpClient(handler);
        using var client = new AnthropicLlmClient(httpClient, ownsHttpClient: false);

        var verdicts = await client.ReviewAsync([Question(0)]);

        Assert.Equal(SemanticVerdictKind.Unknown, Assert.Single(verdicts).Kind);
    }

    [Fact]
    public async Task ANonSuccessStatusCodeThrowsAnLlmClientExceptionRatherThanSilentlyProceeding()
    {
        var errorJson = """{"error":{"type":"invalid_request_error","message":"bad request"}}""";
        var handler = new StubHttpMessageHandler((_, _) => JsonResponse(HttpStatusCode.BadRequest, errorJson));

        using var httpClient = new HttpClient(handler);
        using var client = new AnthropicLlmClient(httpClient, ownsHttpClient: false);

        var exception = await Assert.ThrowsAsync<LlmClientException>(() => client.ReviewAsync([Question(0)]));
        Assert.Contains("bad request", exception.Message);
    }

    [Fact]
    public async Task NoQuestionsMakesNoHttpCallAtAll()
    {
        var called = false;
        var handler = new StubHttpMessageHandler((_, _) =>
        {
            called = true;
            return JsonResponse(HttpStatusCode.OK, "{}");
        });

        using var httpClient = new HttpClient(handler);
        using var client = new AnthropicLlmClient(httpClient, ownsHttpClient: false);

        var verdicts = await client.ReviewAsync([]);

        Assert.Empty(verdicts);
        Assert.False(called);
    }
}
