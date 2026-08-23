using System.Text;
using SpecDrift.Core.Model;
using SpecDrift.Core.SpecReading;

namespace SpecDrift.Core.Tests;

public class SpecReaderTests
{
    [Fact]
    public void ParametersAreOrderedPathLevelFirstThenOperationLevel()
    {
        const string yaml = """
            openapi: 3.0.3
            info:
              title: Test
              version: "1.0"
            paths:
              /items/{id}:
                parameters:
                  - name: id
                    in: path
                    required: true
                    schema:
                      type: string
                get:
                  parameters:
                    - name: verbose
                      in: query
                      required: false
                      schema:
                        type: boolean
                  responses:
                    '200':
                      description: ok
                    'default':
                      description: unexpected error
            """;

        var surface = SpecReader.Read(ToStream(yaml)).Surface;
        var endpoint = Assert.Single(surface.Endpoints);

        Assert.Equal(["id", "verbose"], endpoint.Parameters.Select(p => p.Name));
        Assert.Equal(ParameterLocation.Path, endpoint.Parameters[0].Location);
        Assert.Equal(ParameterLocation.Query, endpoint.Parameters[1].Location);

        // "default" names no single status code a return statement could match against,
        // so it's skipped rather than guessed at.
        var response = Assert.Single(endpoint.Responses);
        Assert.Equal(200, response.StatusCode);
        Assert.Null(response.SchemaName);
    }

    [Fact]
    public void UnresolvableReference_ThrowsSpecParseExceptionRatherThanGuessing()
    {
        const string yaml = """
            openapi: 3.0.3
            info:
              title: Test
              version: "1.0"
            paths:
              /widgets:
                get:
                  responses:
                    '200':
                      description: ok
                      content:
                        application/json:
                          schema:
                            $ref: '#/components/schemas/DoesNotExist'
            """;

        var exception = Assert.Throws<SpecParseException>(() => SpecReader.Read(ToStream(yaml)));
        Assert.NotEmpty(exception.Errors);
    }

    private static MemoryStream ToStream(string content) => new(Encoding.UTF8.GetBytes(content));
}
