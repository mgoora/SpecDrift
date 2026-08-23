namespace SpecDrift.Core.Model;

/// <summary>The known dialect descriptors.</summary>
public static class DialectDescriptors
{
    public static readonly DialectDescriptor WebApi2 = new(
        Dialect: DialectChoice.WebApi2,
        ControllerBaseClassNames: ["ApiController"],
        RoutePrefixAttributeName: "RoutePrefix",
        RouteAttributeName: "Route",
        HttpMethodAttributeNames: new Dictionary<string, HttpVerb>
        {
            ["HttpGet"] = HttpVerb.Get,
            ["HttpPost"] = HttpVerb.Post,
            ["HttpPut"] = HttpVerb.Put,
            ["HttpPatch"] = HttpVerb.Patch,
            ["HttpDelete"] = HttpVerb.Delete,
            ["HttpHead"] = HttpVerb.Head,
            ["HttpOptions"] = HttpVerb.Options,
        },
        InfersHttpMethodFromNamePrefix: true,
        ResponseTypeAttributeName: "ResponseType",
        RequestBodyBindingAttributeName: "FromBody",
        QueryBindingAttributeName: "FromUri",
        StatusHelperMethods: new Dictionary<string, int?>
        {
            ["Ok"] = 200,
            ["Created"] = 201,
            ["CreatedAtRoute"] = 201,
            ["NoContent"] = 204,
            ["BadRequest"] = 400,
            ["Unauthorized"] = 401,
            ["NotFound"] = 404,
            ["Conflict"] = 409,
            ["StatusCode"] = null, // argument-dependent — resolved via StatusCodeConstants
        },
        StatusCodeConstants: new Dictionary<string, int>
        {
            // System.Net.HttpStatusCode member names, as passed to StatusCode(...).
            ["OK"] = 200,
            ["Created"] = 201,
            ["NoContent"] = 204,
            ["BadRequest"] = 400,
            ["Unauthorized"] = 401,
            ["Forbidden"] = 403,
            ["NotFound"] = 404,
            ["Conflict"] = 409,
        });

    public static readonly DialectDescriptor Core = new(
        Dialect: DialectChoice.Core,
        ControllerBaseClassNames: ["ControllerBase", "Controller"],
        // ASP.NET Core has no dedicated route-prefix attribute — the controller-level
        // prefix is written with the same [Route(...)] attribute as an action's own
        // route, just applied to the class instead of the method.
        RoutePrefixAttributeName: "Route",
        RouteAttributeName: "Route",
        HttpMethodAttributeNames: new Dictionary<string, HttpVerb>
        {
            ["HttpGet"] = HttpVerb.Get,
            ["HttpPost"] = HttpVerb.Post,
            ["HttpPut"] = HttpVerb.Put,
            ["HttpPatch"] = HttpVerb.Patch,
            ["HttpDelete"] = HttpVerb.Delete,
            ["HttpHead"] = HttpVerb.Head,
            ["HttpOptions"] = HttpVerb.Options,
        },
        InfersHttpMethodFromNamePrefix: false, // Core has no such convention — attributes only
        ResponseTypeAttributeName: "ProducesResponseType",
        RequestBodyBindingAttributeName: "FromBody",
        QueryBindingAttributeName: "FromQuery",
        StatusHelperMethods: new Dictionary<string, int?>
        {
            ["Ok"] = 200,
            ["Created"] = 201,
            ["CreatedAtAction"] = 201,
            ["CreatedAtRoute"] = 201,
            ["NoContent"] = 204,
            ["BadRequest"] = 400,
            ["Unauthorized"] = 401,
            ["Forbidden"] = 403,
            ["NotFound"] = 404,
            ["Conflict"] = 409,
            ["StatusCode"] = null,  // argument-dependent — resolved via StatusCodeConstants
            ["Problem"] = null,     // a real ControllerBase method here, unlike WebApi2's
                                    // same-named local helper — also argument-dependent
        },
        StatusCodeConstants: new Dictionary<string, int>
        {
            // Microsoft.AspNetCore.Http.StatusCodes member names, as passed to
            // StatusCode(...) or Problem(statusCode: ...).
            ["Status200OK"] = 200,
            ["Status201Created"] = 201,
            ["Status204NoContent"] = 204,
            ["Status400BadRequest"] = 400,
            ["Status401Unauthorized"] = 401,
            ["Status403Forbidden"] = 403,
            ["Status404NotFound"] = 404,
            ["Status409Conflict"] = 409,
        },
        ExpandsControllerAndActionRouteTokens: true,
        ControllerNameSuffixToStrip: "Controller",
        HttpMethodAttributesEstablishRouting: true,
        StatusCodeNamedArgumentName: "statusCode");
}
