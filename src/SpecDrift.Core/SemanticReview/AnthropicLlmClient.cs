using System.Text;
using System.Text.Json;
using SpecDrift.Core.Model;

namespace SpecDrift.Core.SemanticReview;

/// <summary>
/// SD100's LLM call — one non-streaming POST to Anthropic's Messages API per run,
/// bundling every <see cref="SemanticQuestion"/> into a single request. Plain
/// <see cref="HttpClient"/>, no SDK package: the one seam in the tool that leaves the
/// process, kept exactly as small and inspectable as everything else. Model is
/// <c>claude-haiku-4-5</c> — SD100 is one low-stakes binary judgment per endpoint,
/// always reported Low severity / Probable confidence, so cheap-and-good-enough fits
/// better than frontier reasoning spent on it.
/// </summary>
public sealed class AnthropicLlmClient : ILlmClient, IDisposable
{
    private const string Model = "claude-haiku-4-5";
    private const string ApiVersion = "2023-06-01";
    private const string Endpoint = "https://api.anthropic.com/v1/messages";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static readonly JsonElement VerdictsSchema = JsonSerializer.Deserialize<JsonElement>("""
        {
          "type": "object",
          "properties": {
            "verdicts": {
              "type": "array",
              "items": {
                "type": "object",
                "properties": {
                  "index": { "type": "integer" },
                  "verdict": { "type": "string", "enum": ["contradicts", "consistent", "unknown"] },
                  "explanation": { "type": "string" }
                },
                "required": ["index", "verdict", "explanation"],
                "additionalProperties": false
              }
            }
          },
          "required": ["verdicts"],
          "additionalProperties": false
        }
        """);

    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;

    public AnthropicLlmClient(string apiKey) : this(CreateHttpClient(apiKey), ownsHttpClient: true)
    {
    }

    /// <summary>Test-only seam: pass a client wrapping a stubbed handler so the
    /// request/response contract can be verified without a real network call.</summary>
    internal AnthropicLlmClient(HttpClient httpClient, bool ownsHttpClient)
    {
        _httpClient = httpClient;
        _ownsHttpClient = ownsHttpClient;
    }

    public async Task<IReadOnlyList<SemanticVerdict>> ReviewAsync(
        IReadOnlyList<SemanticQuestion> questions, CancellationToken cancellationToken = default)
    {
        if (questions.Count == 0)
        {
            return [];
        }

        var request = BuildRequest(questions);
        var requestJson = JsonSerializer.Serialize(request, JsonOptions);

        using var content = new StringContent(requestJson, Encoding.UTF8, "application/json");
        using var httpResponse = await _httpClient.PostAsync(Endpoint, content, cancellationToken);
        var body = await httpResponse.Content.ReadAsStringAsync(cancellationToken);

        if (!httpResponse.IsSuccessStatusCode)
        {
            var apiMessage = TryReadErrorMessage(body);
            throw new LlmClientException(
                $"Anthropic API request failed with status {(int)httpResponse.StatusCode}" +
                (apiMessage is null ? $": {body}" : $": {apiMessage}"));
        }

        var response = JsonSerializer.Deserialize<MessageResponse>(body, JsonOptions);
        var text = response?.Content?.FirstOrDefault(c => c.Type == "text")?.Text;

        if (text is null)
        {
            throw new LlmClientException("Anthropic API response had no text content to parse.");
        }

        var payload = JsonSerializer.Deserialize<VerdictsPayload>(text, JsonOptions)
            ?? throw new LlmClientException("Anthropic API's structured output could not be parsed as JSON.");

        return payload.Verdicts.Select(ToVerdict).ToList();
    }

    private static MessageRequest BuildRequest(IReadOnlyList<SemanticQuestion> questions) => new(
        Model,
        MaxTokens: 4096,
        System: BuildSystemPrompt(),
        Messages: [new MessageParam("user", BuildUserContent(questions))],
        OutputConfig: new OutputConfig(new OutputFormat("json_schema", VerdictsSchema)));

    private static string BuildSystemPrompt() =>
        "You review whether an OpenAPI endpoint's documented description still matches what its " +
        "handler code actually does. For each numbered item below you are given the specification's " +
        "description and the full source of the C# action method implementing it. For each item, decide " +
        "whether the description contradicts the code's actual behaviour, is consistent with it, or " +
        "whether you cannot tell from what's given. Respond only with the requested structured output.";

    private static string BuildUserContent(IReadOnlyList<SemanticQuestion> questions)
    {
        var builder = new StringBuilder();

        foreach (var question in questions)
        {
            builder.AppendLine($"### Item {question.Index}: {question.Method} {question.RouteTemplate}");
            builder.AppendLine();
            builder.AppendLine("Specification description:");
            builder.AppendLine(question.SpecDescription);
            builder.AppendLine();
            builder.AppendLine("Handler source:");
            builder.AppendLine("```csharp");
            builder.AppendLine(question.CodeSourceText);
            builder.AppendLine("```");
            builder.AppendLine();
        }

        return builder.ToString();
    }

    private static SemanticVerdict ToVerdict(VerdictDto dto) => new(
        dto.Index,
        dto.Verdict switch
        {
            "contradicts" => SemanticVerdictKind.Contradicts,
            "consistent" => SemanticVerdictKind.Consistent,
            _ => SemanticVerdictKind.Unknown // an unrecognised label is Unknown, not guessed at
        },
        dto.Explanation);

    private static string? TryReadErrorMessage(string body)
    {
        try
        {
            return JsonSerializer.Deserialize<ApiErrorEnvelope>(body, JsonOptions)?.Error?.Message;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static HttpClient CreateHttpClient(string apiKey)
    {
        var client = new HttpClient();
        client.DefaultRequestHeaders.Add("x-api-key", apiKey);
        client.DefaultRequestHeaders.Add("anthropic-version", ApiVersion);
        return client;
    }

    public void Dispose()
    {
        if (_ownsHttpClient)
        {
            _httpClient.Dispose();
        }
    }
}
