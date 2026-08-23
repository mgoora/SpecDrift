using System.Text.Json;
using System.Text.Json.Serialization;

namespace SpecDrift.Core.SemanticReview;

// The Anthropic Messages API's wire shapes — only the fields this client actually
// sends or reads. Deliberately minimal: this is a plain HttpClient call, not the
// SDK, so there's no generated model to reuse.

internal sealed record MessageRequest(
    [property: JsonPropertyName("model")] string Model,
    [property: JsonPropertyName("max_tokens")] int MaxTokens,
    [property: JsonPropertyName("system")] string System,
    [property: JsonPropertyName("messages")] IReadOnlyList<MessageParam> Messages,
    [property: JsonPropertyName("output_config")] OutputConfig OutputConfig);

internal sealed record MessageParam(
    [property: JsonPropertyName("role")] string Role,
    [property: JsonPropertyName("content")] string Content);

internal sealed record OutputConfig(
    [property: JsonPropertyName("format")] OutputFormat Format);

internal sealed record OutputFormat(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("schema")] JsonElement Schema);

internal sealed record MessageResponse(
    [property: JsonPropertyName("content")] IReadOnlyList<ContentBlock>? Content);

internal sealed record ContentBlock(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("text")] string? Text);

internal sealed record ApiErrorEnvelope(
    [property: JsonPropertyName("error")] ApiError? Error);

internal sealed record ApiError(
    [property: JsonPropertyName("type")] string? Type,
    [property: JsonPropertyName("message")] string? Message);

internal sealed record VerdictsPayload(
    [property: JsonPropertyName("verdicts")] IReadOnlyList<VerdictDto> Verdicts);

internal sealed record VerdictDto(
    [property: JsonPropertyName("index")] int Index,
    [property: JsonPropertyName("verdict")] string Verdict,
    [property: JsonPropertyName("explanation")] string Explanation);
