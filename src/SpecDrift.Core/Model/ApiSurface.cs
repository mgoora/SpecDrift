namespace SpecDrift.Core.Model;

/// <summary>
/// A parameter bound by an endpoint. <see cref="SchemaType"/> is the referenced
/// component schema's name if the parameter's schema is a <c>$ref</c>, otherwise its
/// inline OpenAPI type (e.g. <c>string</c>); null if the reader could not determine one.
/// </summary>
public sealed record ApiParameter(
    string Name,
    ParameterLocation Location,
    bool Required,
    string? SchemaType,
    SourceCitation Citation);

/// <summary>The request body's declared shape — <see cref="SchemaName"/> follows the same
/// naming rule as <see cref="ApiParameter.SchemaType"/>.</summary>
public sealed record RequestBodyShape(
    string? SchemaName,
    bool Required,
    SourceCitation Citation);

/// <summary>
/// A declared or observed response. The spec side always has a status code — every
/// response is keyed by one in the document. The code side does not always: Web API 2's
/// <c>[ResponseType(typeof(T))]</c> declares a body shape without pinning it to a
/// specific status code, so <see cref="StatusCode"/> is null for that entry — a
/// separate entry (<see cref="SchemaName"/> null, <see cref="StatusCode"/> set) records
/// each status code actually observed being returned from the method body.
/// </summary>
public sealed record ApiResponse(
    int? StatusCode,
    string? SchemaName,
    SourceCitation Citation);

/// <summary>One operation — a route template plus HTTP method — with everything the
/// comparer needs to match and diff it against its counterpart on the other side.
/// <see cref="OperationName"/> is the spec's <c>operationId</c> on the spec side, or
/// the C# method name on the code side — a secondary identity signal the comparer
/// uses when a route's methods don't line up cleanly by position alone (see SD003).
/// <see cref="HasUnresolvedResponses"/> is code-side only (always false from the spec
/// reader): true when at least one return statement in the action couldn't be
/// resolved to a status code. SD008 ("never returned") is gated on this being false —
/// "never returns X" isn't a claim the comparer can stand behind when some of the
/// action's own return paths are already known blind spots.
///
/// <see cref="SpecDescription"/> (spec-side only) and <see cref="CodeSourceText"/>
/// (code-side only) exist for exactly one purpose: letting the comparer build a
/// <see cref="SemanticQuestion"/> for SD100 when structural comparison has nothing
/// left to say about a matched pair — whether the spec's own prose still describes
/// what the handler does is not a question parsing can answer.</summary>
public sealed record ApiEndpoint(
    string RouteTemplate,
    HttpVerb Method,
    SourceCitation Citation,
    IReadOnlyList<ApiParameter> Parameters,
    RequestBodyShape? RequestBody,
    IReadOnlyList<ApiResponse> Responses,
    string? OperationName,
    bool HasUnresolvedResponses = false,
    string? SpecDescription = null,
    string? CodeSourceText = null);

/// <summary>
/// The normalised shape both the spec reader and the code reader produce — the
/// comparer never knows or cares which side an <see cref="ApiSurface"/> came from.
/// </summary>
public sealed record ApiSurface(IReadOnlyList<ApiEndpoint> Endpoints);
