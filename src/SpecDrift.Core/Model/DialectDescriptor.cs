namespace SpecDrift.Core.Model;

/// <summary>
/// The closed set of framework-specific facts the code reader's single walker needs:
/// attribute names, base classes, binding conventions, status-helper methods. The
/// dialect rule this exists to serve: one walker, two descriptors, never two walkers —
/// if a per-dialect difference can't be expressed as data here, that's a design
/// failure to flag, not a place to fork the traversal.
/// </summary>
public sealed record DialectDescriptor(
    DialectChoice Dialect,
    IReadOnlyList<string> ControllerBaseClassNames,
    string? RoutePrefixAttributeName,
    string RouteAttributeName,
    IReadOnlyDictionary<string, HttpVerb> HttpMethodAttributeNames,
    bool InfersHttpMethodFromNamePrefix,
    string ResponseTypeAttributeName,
    string RequestBodyBindingAttributeName,
    string? QueryBindingAttributeName,
    IReadOnlyDictionary<string, int?> StatusHelperMethods,
    IReadOnlyDictionary<string, int> StatusCodeConstants,
    // ASP.NET Core only: [controller]/[action] route tokens (WebApi2 has none — both
    // false/null here for WebApi2, and the walker's expansion step is a no-op).
    bool ExpandsControllerAndActionRouteTokens = false,
    string? ControllerNameSuffixToStrip = null,
    // True (Core) when a bare HTTP-verb attribute — [HttpGet], no argument — is
    // enough on its own to attribute-route the action (its own route segment is then
    // simply empty). False (WebApi2): a verb attribute alone never establishes
    // routing; without a companion [Route] the action is convention-routed and
    // genuinely unknown.
    bool HttpMethodAttributesEstablishRouting = false,
    // ASP.NET Core's Problem(statusCode: ..., ...) names its status argument; WebApi2's
    // StatusCode(HttpStatusCode.X) never does. Null means "go by position only".
    string? StatusCodeNamedArgumentName = null);
