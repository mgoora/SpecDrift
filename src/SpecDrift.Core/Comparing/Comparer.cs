using System.Text.RegularExpressions;
using SpecDrift.Core.CodeReading;
using SpecDrift.Core.Model;
using SpecDrift.Core.SpecReading;

namespace SpecDrift.Core.Comparing;

/// <summary>
/// Stage 3 of the pipeline: matches the spec's and code's <see cref="ApiSurface"/>s
/// by normalised route + method and emits deterministic findings — SD001-SD003 on
/// unmatched endpoints, SD004-SD009 on matched ones.
/// </summary>
public static class Comparer
{
    private static readonly Regex RouteConstraintPattern = new(@"\{([^:}]+):[^}]+\}", RegexOptions.Compiled);

    public static ComparisonResult Compare(SpecReadResult specResult, CodeReadResult codeResult, DialectChoice dialect)
    {
        var spec = specResult.Surface;
        var code = codeResult.Surface;
        var findings = new List<DriftFinding>();
        var semanticQuestions = new List<SemanticQuestion>();

        var routes = spec.Endpoints.Select(e => NormalizeRoute(e.RouteTemplate))
            .Concat(code.Endpoints.Select(e => NormalizeRoute(e.RouteTemplate)))
            .Distinct(StringComparer.Ordinal);

        foreach (var route in routes)
        {
            var specAtRoute = spec.Endpoints.Where(e => NormalizeRoute(e.RouteTemplate) == route).ToList();
            var codeAtRoute = code.Endpoints.Where(e => NormalizeRoute(e.RouteTemplate) == route).ToList();

            var matchedMethods = new HashSet<HttpVerb>(
                specAtRoute.Select(e => e.Method).Intersect(codeAtRoute.Select(e => e.Method)));

            foreach (var method in matchedMethods)
            {
                var specEndpoint = specAtRoute.First(e => e.Method == method);
                var codeEndpoint = codeAtRoute.First(e => e.Method == method);

                CompareParameters(specEndpoint, codeEndpoint, dialect, findings);
                CompareResponses(specEndpoint, codeEndpoint, dialect, specResult.Schemas, codeResult.Types, findings);
                CollectSemanticQuestion(specEndpoint, codeEndpoint, semanticQuestions);
            }

            var unmatchedSpec = specAtRoute.Where(e => !matchedMethods.Contains(e.Method)).ToList();
            var unmatchedCode = codeAtRoute.Where(e => !matchedMethods.Contains(e.Method)).ToList();

            // A route with exactly one unmatched method on each side, whose declared
            // identity (operationId / C# method name) still agrees, reads as "this
            // operation's method changed" (SD003) rather than two unrelated endpoints
            // that happen to share a path — the latter is genuinely two separate
            // findings (SD001 + SD002), not one. Anything else is ambiguous enough
            // that guessing a pairing would be worse than reporting each side plainly.
            if (unmatchedSpec.Count == 1 && unmatchedCode.Count == 1
                && NamesLikelyMatch(unmatchedSpec[0].OperationName, unmatchedCode[0].OperationName))
            {
                findings.Add(BuildMethodMismatch(unmatchedSpec[0], unmatchedCode[0], dialect));

                // Still the same operation underneath the method change — its
                // parameters and response can independently disagree too.
                CompareParameters(unmatchedSpec[0], unmatchedCode[0], dialect, findings);
                CompareResponses(unmatchedSpec[0], unmatchedCode[0], dialect, specResult.Schemas, codeResult.Types, findings);
                CollectSemanticQuestion(unmatchedSpec[0], unmatchedCode[0], semanticQuestions);
                continue;
            }

            foreach (var specEndpoint in unmatchedSpec)
            {
                findings.Add(BuildMissingEndpoint(specEndpoint, dialect));
            }

            foreach (var codeEndpoint in unmatchedCode)
            {
                findings.Add(BuildUndocumentedEndpoint(codeEndpoint, dialect));
            }
        }

        var orderedFindings = findings
            .OrderByDescending(f => f.Severity)
            .ThenBy(f => f.Code, StringComparer.Ordinal)
            .ThenBy(f => f.Title, StringComparer.Ordinal)
            .ToList();

        return new ComparisonResult(orderedFindings, semanticQuestions);
    }

    /// <summary>
    /// SD100's residue: a matched endpoint with both a spec description and readable
    /// code is a candidate for "does the prose still describe the handler?" — a
    /// question structural comparison cannot itself answer. Silently skipped when
    /// either side has nothing to ask about (an endpoint with no description, or code
    /// whose source somehow wasn't captured) rather than asking a question with half
    /// its evidence missing.
    /// </summary>
    private static void CollectSemanticQuestion(ApiEndpoint specEndpoint, ApiEndpoint codeEndpoint, List<SemanticQuestion> questions)
    {
        if (string.IsNullOrWhiteSpace(specEndpoint.SpecDescription) || string.IsNullOrWhiteSpace(codeEndpoint.CodeSourceText))
        {
            return;
        }

        questions.Add(new SemanticQuestion(
            questions.Count,
            specEndpoint.RouteTemplate,
            specEndpoint.Method,
            specEndpoint.SpecDescription,
            codeEndpoint.CodeSourceText,
            specEndpoint.Citation,
            codeEndpoint.Citation));
    }

    private static void CompareParameters(ApiEndpoint specEndpoint, ApiEndpoint codeEndpoint, DialectChoice dialect, List<DriftFinding> findings)
    {
        foreach (var codeParam in codeEndpoint.Parameters)
        {
            var matchesSpec = specEndpoint.Parameters.Any(p => string.Equals(p.Name, codeParam.Name, StringComparison.OrdinalIgnoreCase));
            if (!matchesSpec)
            {
                findings.Add(BuildUndocumentedParameter(specEndpoint, codeParam, dialect));
            }
        }

        foreach (var specParam in specEndpoint.Parameters)
        {
            var codeParam = codeEndpoint.Parameters.FirstOrDefault(p => string.Equals(p.Name, specParam.Name, StringComparison.OrdinalIgnoreCase));

            if (codeParam is null)
            {
                findings.Add(BuildMissingParameter(specParam, codeEndpoint, dialect));
            }
            else if (specParam.Required != codeParam.Required)
            {
                findings.Add(BuildRequirednessMismatch(specParam, codeParam, dialect));
            }
        }
    }

    private static void CompareResponses(
        ApiEndpoint specEndpoint,
        ApiEndpoint codeEndpoint,
        DialectChoice dialect,
        IReadOnlyDictionary<string, SchemaShape> specSchemas,
        IReadOnlyDictionary<string, SchemaShape> codeTypes,
        List<DriftFinding> findings)
    {
        var specCodes = specEndpoint.Responses
            .Where(r => r.StatusCode.HasValue)
            .ToDictionary(r => r.StatusCode!.Value, r => r);

        var observedByCode = codeEndpoint.Responses
            .Where(r => r.StatusCode.HasValue)
            .GroupBy(r => r.StatusCode!.Value)
            .ToDictionary(g => g.Key, g => g.First());

        foreach (var (code, response) in observedByCode)
        {
            if (!specCodes.ContainsKey(code))
            {
                findings.Add(BuildUndocumentedStatusCode(specEndpoint, response, code, dialect));
            }
        }

        // "Never returned" is a claim about every path through the action — not
        // defensible when some of its own return statements are already a known
        // blind spot (see ApiEndpoint.HasUnresolvedResponses).
        if (!codeEndpoint.HasUnresolvedResponses)
        {
            foreach (var (specCode, specResponse) in specCodes)
            {
                if (!observedByCode.ContainsKey(specCode))
                {
                    findings.Add(BuildUndeliveredStatusCode(specResponse, codeEndpoint, specCode, dialect));
                }
            }
        }

        CompareResponseType(specEndpoint, codeEndpoint, dialect, specSchemas, codeTypes, findings);
    }

    /// <summary>
    /// SD009. A declared response type can disagree at two levels: the declared type's
    /// name itself differs, or — the harder case, and the one this fixture actually
    /// plants — the name matches on both sides while a property underneath it (e.g. a
    /// list's element type) has quietly changed. The first is caught by a name
    /// comparison; the second needs each side's named schema resolved to its own
    /// property shape and compared property by property.
    ///
    /// This walk is deliberately exactly one property deep, permanently — not a
    /// depth limit to lift later. Neither fixture has ever needed a second hop (e.g.
    /// diffing <c>ClientSummary</c>'s own properties against <c>Client</c>'s), and
    /// building general recursive schema comparison — with cycle detection for
    /// self-referencing schemas — against no real case would be exactly the
    /// speculative abstraction the project rules rule out. If a fixture ever plants a
    /// two-hop drift, that's a real case to design against; until then, one hop.
    /// </summary>
    private static void CompareResponseType(
        ApiEndpoint specEndpoint,
        ApiEndpoint codeEndpoint,
        DialectChoice dialect,
        IReadOnlyDictionary<string, SchemaShape> specSchemas,
        IReadOnlyDictionary<string, SchemaShape> codeTypes,
        List<DriftFinding> findings)
    {
        // Web API 2's [ResponseType(typeof(T))] never pins a status code — one entry,
        // StatusCode null, matched against the spec's primary (lowest) 2xx response.
        // ASP.NET Core's [ProducesResponseType(typeof(T), code)] pins one directly, and
        // an action can carry several — each is matched against the spec response at
        // that exact code instead of guessing which one is "primary".
        foreach (var declaredType in codeEndpoint.Responses.Where(r => r.SchemaName is not null))
        {
            var matchingSpecResponse = declaredType.StatusCode is { } pinnedCode
                ? specEndpoint.Responses.FirstOrDefault(r => r.StatusCode == pinnedCode)
                : specEndpoint.Responses
                    .Where(r => r.StatusCode is >= 200 and < 300 && r.SchemaName is not null)
                    .OrderBy(r => r.StatusCode)
                    .FirstOrDefault();

            if (matchingSpecResponse?.SchemaName is null)
            {
                continue;
            }

            CompareOneResponseType(declaredType, matchingSpecResponse, dialect, specSchemas, codeTypes, findings);
        }
    }

    private static void CompareOneResponseType(
        ApiResponse declaredType,
        ApiResponse matchingSpecResponse,
        DialectChoice dialect,
        IReadOnlyDictionary<string, SchemaShape> specSchemas,
        IReadOnlyDictionary<string, SchemaShape> codeTypes,
        List<DriftFinding> findings)
    {
        if (!string.Equals(matchingSpecResponse.SchemaName, declaredType.SchemaName, StringComparison.Ordinal))
        {
            findings.Add(new DriftFinding(
                "SD009",
                $"Response type mismatch: `{declaredType.SchemaName}` vs `{matchingSpecResponse.SchemaName}`",
                Severity.Medium,
                Confidence.Certain,
                dialect,
                matchingSpecResponse.Citation,
                declaredType.Citation,
                $"The code declares its response as `{declaredType.SchemaName}`, but the specification declares " +
                $"`{matchingSpecResponse.SchemaName}` for this operation's {matchingSpecResponse.StatusCode} response."));
            return;
        }

        if (!specSchemas.TryGetValue(matchingSpecResponse.SchemaName!, out var specShape)
            || !codeTypes.TryGetValue(declaredType.SchemaName!, out var codeShape))
        {
            return;
        }

        foreach (var (propertyName, specProperty) in specShape.Properties)
        {
            if (!specProperty.IsSchemaReference)
            {
                // A plain OpenAPI primitive (string/format:uuid, string/format:date, …)
                // has no equivalent-CLR-type table behind it here — comparing it
                // against C#'s Guid/DateTime would be a false positive, not a finding.
                continue;
            }

            if (!codeShape.Properties.TryGetValue(propertyName, out var codeProperty))
            {
                continue; // a property's own presence/absence is a parameter-shaped
                          // question this pass doesn't answer, not a type mismatch
            }

            if (!string.Equals(specProperty.TypeDescription, codeProperty.TypeDescription, StringComparison.Ordinal))
            {
                findings.Add(new DriftFinding(
                    "SD009",
                    $"Response type mismatch: `{declaredType.SchemaName}.{propertyName}`",
                    Severity.Medium,
                    Confidence.Certain,
                    dialect,
                    specProperty.Citation,
                    codeProperty.Citation,
                    $"The specification's `{declaredType.SchemaName}.{propertyName}` is `{specProperty.TypeDescription}`, " +
                    $"but the code's is `{codeProperty.TypeDescription}`."));
            }
        }
    }

    private static DriftFinding BuildMissingEndpoint(ApiEndpoint specEndpoint, DialectChoice dialect) => new(
        Code: "SD001",
        Title: $"Missing endpoint `{Describe(specEndpoint)}`",
        Severity: Severity.High,
        Confidence: Confidence.Certain,
        Dialect: dialect,
        SpecCitation: specEndpoint.Citation,
        CodeCitation: null,
        Detail: $"The specification declares `{Describe(specEndpoint)}`, but no action in the analysed project handles it.");

    private static DriftFinding BuildUndocumentedEndpoint(ApiEndpoint codeEndpoint, DialectChoice dialect) => new(
        Code: "SD002",
        Title: $"Undocumented endpoint `{Describe(codeEndpoint)}`",
        Severity: Severity.High,
        Confidence: Confidence.Certain,
        Dialect: dialect,
        SpecCitation: null,
        CodeCitation: codeEndpoint.Citation,
        Detail: $"`{codeEndpoint.OperationName ?? Describe(codeEndpoint)}` handles `{Describe(codeEndpoint)}`, " +
                "but the specification never declares this endpoint.");

    private static DriftFinding BuildMethodMismatch(ApiEndpoint specEndpoint, ApiEndpoint codeEndpoint, DialectChoice dialect) => new(
        Code: "SD003",
        Title: $"HTTP method mismatch on `{specEndpoint.RouteTemplate}`",
        Severity: Severity.High,
        Confidence: Confidence.Certain,
        Dialect: dialect,
        SpecCitation: specEndpoint.Citation,
        CodeCitation: codeEndpoint.Citation,
        Detail: $"The specification declares `{Describe(specEndpoint)}`, but the code now handles this route " +
                $"with `{codeEndpoint.Method.ToString().ToUpperInvariant()}` instead.");

    private static DriftFinding BuildUndocumentedParameter(ApiEndpoint specEndpoint, ApiParameter codeParam, DialectChoice dialect) => new(
        Code: "SD004",
        Title: $"Undocumented parameter `{codeParam.Name}`",
        Severity: Severity.Medium,
        Confidence: Confidence.Certain,
        Dialect: dialect,
        SpecCitation: specEndpoint.Citation,
        CodeCitation: codeParam.Citation,
        Detail: $"The action binds a {codeParam.Location.ToString().ToLowerInvariant()} parameter `{codeParam.Name}` " +
                "that the specification does not declare.");

    private static DriftFinding BuildMissingParameter(ApiParameter specParam, ApiEndpoint codeEndpoint, DialectChoice dialect) => new(
        Code: "SD005",
        Title: $"Missing parameter `{specParam.Name}`",
        Severity: Severity.Medium,
        Confidence: Confidence.Certain,
        Dialect: dialect,
        SpecCitation: specParam.Citation,
        CodeCitation: codeEndpoint.Citation,
        Detail: $"The specification declares a {specParam.Location.ToString().ToLowerInvariant()} parameter `{specParam.Name}` " +
                "that the action does not bind.");

    private static DriftFinding BuildRequirednessMismatch(ApiParameter specParam, ApiParameter codeParam, DialectChoice dialect) => new(
        Code: "SD006",
        Title: $"Required-ness mismatch on `{specParam.Name}`",
        Severity: Severity.Medium,
        Confidence: Confidence.Certain,
        Dialect: dialect,
        SpecCitation: specParam.Citation,
        CodeCitation: codeParam.Citation,
        Detail: $"The specification marks `{specParam.Name}` as {(specParam.Required ? "required" : "optional")}, " +
                $"but the code makes it {(codeParam.Required ? "required" : "optional")}.");

    private static DriftFinding BuildUndocumentedStatusCode(ApiEndpoint specEndpoint, ApiResponse codeResponse, int code, DialectChoice dialect) => new(
        Code: "SD007",
        Title: $"Undocumented status code `{code}`",
        Severity: Severity.Medium,
        Confidence: Confidence.Certain,
        Dialect: dialect,
        SpecCitation: specEndpoint.Citation,
        CodeCitation: codeResponse.Citation,
        Detail: $"The code returns `{code}`, which the specification does not declare for this operation.");

    private static DriftFinding BuildUndeliveredStatusCode(ApiResponse specResponse, ApiEndpoint codeEndpoint, int code, DialectChoice dialect) => new(
        Code: "SD008",
        Title: $"Undelivered status code `{code}`",
        Severity: Severity.Low,
        Confidence: Confidence.Certain,
        Dialect: dialect,
        SpecCitation: specResponse.Citation,
        CodeCitation: codeEndpoint.Citation,
        Detail: $"The specification declares `{code}` for this operation, but no resolvable path in the code returns it.");

    private static string Describe(ApiEndpoint endpoint) => $"{endpoint.Method.ToString().ToUpperInvariant()} {endpoint.RouteTemplate}";

    private static bool NamesLikelyMatch(string? specOperationName, string? codeOperationName) =>
        specOperationName is not null
        && codeOperationName is not null
        && string.Equals(specOperationName, codeOperationName, StringComparison.OrdinalIgnoreCase);

    /// <summary>Strips Web API 2 / ASP.NET Core route constraints (<c>{id:guid}</c> →
    /// <c>{id}</c>) and normalises slashes and case, so both readers' raw route
    /// templates become comparable without either reader needing to know about the
    /// other's conventions.
    ///
    /// Deliberately the only place this normalisation happens. Each reader keeps
    /// storing (and citing, in report text) the route exactly as its own source
    /// writes it — <c>/api/clients/{id}</c> from the spec, <c>api/clients/{id:guid}</c>
    /// from the controller attribute — which is more useful to a reader checking
    /// against their own file than a canonical form would be. Only the comparer, which
    /// is the one place that actually needs a cross-dialect key to match on, does the
    /// normalising; there is no plan to duplicate a lighter version of it into either
    /// reader.</summary>
    private static string NormalizeRoute(string routeTemplate) =>
        RouteConstraintPattern.Replace(routeTemplate, "{$1}").Trim('/').ToLowerInvariant();
}
