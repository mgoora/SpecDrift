using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Readers;
using SpecDrift.Core.Model;

namespace SpecDrift.Core.SpecReading;

/// <summary>
/// Stage 1 of the pipeline: an OpenAPI document (JSON or YAML — the underlying reader
/// detects the format) into a normalised <see cref="ApiSurface"/>. Every element it
/// produces carries a <see cref="SpecCitation"/> pointing back into the document.
/// </summary>
public static class SpecReader
{
    public static SpecReadResult Read(Stream openApiContent)
    {
        var document = new OpenApiStreamReader().Read(openApiContent, out var diagnostic);

        if (diagnostic.Errors.Count > 0)
        {
            throw new SpecParseException(diagnostic.Errors.Select(e => e.Message).ToList());
        }

        var endpoints = new List<ApiEndpoint>();

        foreach (var (routeTemplate, pathItem) in document.Paths)
        {
            var pathPointer = $"#/paths/{EncodePointerSegment(routeTemplate)}";

            foreach (var (operationType, operation) in pathItem.Operations)
            {
                var verb = ToHttpVerb(operationType);
                var operationPointer = $"{pathPointer}/{verb.ToString().ToLowerInvariant()}";

                var parameters = ReadParameters(pathItem, operation, operationPointer);
                var requestBody = ReadRequestBody(operation, operationPointer);
                var responses = ReadResponses(operation, operationPointer);

                endpoints.Add(new ApiEndpoint(
                    routeTemplate,
                    verb,
                    new SpecCitation(operationPointer),
                    parameters,
                    requestBody,
                    responses,
                    operation.OperationId,
                    SpecDescription: string.IsNullOrWhiteSpace(operation.Description) ? operation.Summary : operation.Description));
            }
        }

        var schemas = ReadSchemaShapes(document.Components?.Schemas);

        return new SpecReadResult(new ApiSurface(endpoints), document.Info.Version, schemas);
    }

    private static Dictionary<string, SchemaShape> ReadSchemaShapes(IDictionary<string, OpenApiSchema>? schemas)
    {
        var result = new Dictionary<string, SchemaShape>(StringComparer.Ordinal);

        if (schemas is null)
        {
            return result;
        }

        foreach (var (schemaName, schema) in schemas)
        {
            var properties = new Dictionary<string, SchemaProperty>(StringComparer.OrdinalIgnoreCase);

            foreach (var (propertyName, propertySchema) in schema.Properties)
            {
                var isReference = propertySchema.Reference is not null
                    || (propertySchema.Type == "array" && propertySchema.Items?.Reference is not null);

                properties[propertyName] = new SchemaProperty(
                    DescribeSchema(propertySchema),
                    isReference,
                    new SpecCitation($"#/components/schemas/{schemaName}/properties/{propertyName}"));
            }

            result[schemaName] = new SchemaShape(schemaName, properties);
        }

        return result;
    }

    private static List<ApiParameter> ReadParameters(OpenApiPathItem pathItem, OpenApiOperation operation, string operationPointer)
    {
        // Path-level parameters (shared across every method on the path) come before
        // the operation's own — matching how they'd be indexed if read back from the document.
        var allParameters = pathItem.Parameters.Concat(operation.Parameters).ToList();
        var result = new List<ApiParameter>(allParameters.Count);

        for (var index = 0; index < allParameters.Count; index++)
        {
            var parameter = allParameters[index];
            result.Add(new ApiParameter(
                parameter.Name,
                ToParameterLocation(parameter.In),
                parameter.Required,
                DescribeSchema(parameter.Schema),
                new SpecCitation($"{operationPointer}/parameters/{index}")));
        }

        return result;
    }

    private static RequestBodyShape? ReadRequestBody(OpenApiOperation operation, string operationPointer)
    {
        if (operation.RequestBody is null)
        {
            return null;
        }

        var schema = operation.RequestBody.Content.Values.FirstOrDefault()?.Schema;

        return new RequestBodyShape(
            DescribeSchema(schema),
            operation.RequestBody.Required,
            new SpecCitation($"{operationPointer}/requestBody"));
    }

    private static List<ApiResponse> ReadResponses(OpenApiOperation operation, string operationPointer)
    {
        var result = new List<ApiResponse>();

        foreach (var (statusCodeKey, response) in operation.Responses)
        {
            // "default" and range keys like "4XX" name no single status code a
            // return statement in code could match against, so they carry nothing
            // the comparer could cite on the code side. Skipped, not guessed at.
            if (!int.TryParse(statusCodeKey, out var statusCode))
            {
                continue;
            }

            var schema = response.Content.Values.FirstOrDefault()?.Schema;

            result.Add(new ApiResponse(
                statusCode,
                DescribeSchema(schema),
                new SpecCitation($"{operationPointer}/responses/{statusCodeKey}")));
        }

        return result;
    }

    private static string? DescribeSchema(OpenApiSchema? schema)
    {
        if (schema is null)
        {
            return null;
        }

        if (schema.Reference is not null)
        {
            return schema.Reference.Id;
        }

        if (schema.Type == "array")
        {
            var itemName = DescribeSchema(schema.Items);
            return itemName is null ? "array" : $"array<{itemName}>";
        }

        return schema.Type;
    }

    private static HttpVerb ToHttpVerb(OperationType operationType) => operationType switch
    {
        OperationType.Get => HttpVerb.Get,
        OperationType.Post => HttpVerb.Post,
        OperationType.Put => HttpVerb.Put,
        OperationType.Patch => HttpVerb.Patch,
        OperationType.Delete => HttpVerb.Delete,
        OperationType.Head => HttpVerb.Head,
        OperationType.Options => HttpVerb.Options,
        OperationType.Trace => HttpVerb.Trace,
        _ => throw new NotSupportedException($"Unsupported OpenAPI operation type: {operationType}")
    };

    private static Model.ParameterLocation ToParameterLocation(Microsoft.OpenApi.Models.ParameterLocation? location) => location switch
    {
        Microsoft.OpenApi.Models.ParameterLocation.Path => Model.ParameterLocation.Path,
        Microsoft.OpenApi.Models.ParameterLocation.Query => Model.ParameterLocation.Query,
        Microsoft.OpenApi.Models.ParameterLocation.Header => Model.ParameterLocation.Header,
        Microsoft.OpenApi.Models.ParameterLocation.Cookie => Model.ParameterLocation.Cookie,
        _ => throw new NotSupportedException($"Unsupported OpenAPI parameter location: {location}")
    };

    /// <summary>JSON Pointer escaping (RFC 6901): <c>~</c> to <c>~0</c>, then <c>/</c> to <c>~1</c>.</summary>
    private static string EncodePointerSegment(string segment) =>
        segment.Replace("~", "~0").Replace("/", "~1");
}
