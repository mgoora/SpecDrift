using SpecDrift.Core.CodeReading;
using SpecDrift.Core.Model;

namespace SpecDrift.Core.Tests;

public class CodeReaderTests : IDisposable
{
    private readonly string _projectDir;

    public CodeReaderTests()
    {
        _projectDir = Path.Combine(Path.GetTempPath(), "specdrift-code-reader-tests-" + Guid.NewGuid());
        Directory.CreateDirectory(_projectDir);
    }

    public void Dispose() => Directory.Delete(_projectDir, recursive: true);

    private void WriteSource(string relativePath, string content)
    {
        var fullPath = Path.Combine(_projectDir, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, content);
    }

    private CodeReadResult ReadWebApi2() => CodeReader.Read(_projectDir, DialectDescriptors.WebApi2);

    [Fact]
    public void CombinesRoutePrefixAndActionRoute()
    {
        WriteSource("Controllers/WidgetsController.cs", """
            using System.Web.Http;

            namespace Test.Controllers
            {
                [RoutePrefix("api/widgets")]
                public class WidgetsController : ApiController
                {
                    [HttpGet]
                    [Route("{id}")]
                    public IHttpActionResult GetWidget(string id)
                    {
                        return Ok(id);
                    }
                }
            }
            """);

        var endpoint = Assert.Single(ReadWebApi2().Surface.Endpoints);

        Assert.Equal("api/widgets/{id}", endpoint.RouteTemplate);
        Assert.Equal(HttpVerb.Get, endpoint.Method);
    }

    [Fact]
    public void InfersHttpMethodFromNamePrefixWhenNoAttributePresent()
    {
        WriteSource("Controllers/WidgetsController.cs", """
            using System.Web.Http;

            namespace Test.Controllers
            {
                public class WidgetsController : ApiController
                {
                    [Route("api/widgets")]
                    public IHttpActionResult GetAllWidgets()
                    {
                        return Ok();
                    }
                }
            }
            """);

        var endpoint = Assert.Single(ReadWebApi2().Surface.Endpoints);
        Assert.Equal(HttpVerb.Get, endpoint.Method);
    }

    [Fact]
    public void PathParameterIsDetectedByRouteTokenMatch()
    {
        WriteSource("Controllers/WidgetsController.cs", """
            using System.Web.Http;

            namespace Test.Controllers
            {
                [RoutePrefix("api/widgets")]
                public class WidgetsController : ApiController
                {
                    [HttpGet]
                    [Route("{id}")]
                    public IHttpActionResult GetWidget(System.Guid id)
                    {
                        return Ok(id);
                    }
                }
            }
            """);

        var endpoint = Assert.Single(ReadWebApi2().Surface.Endpoints);
        var param = Assert.Single(endpoint.Parameters);
        Assert.Equal("id", param.Name);
        Assert.Equal(ParameterLocation.Path, param.Location);
        Assert.True(param.Required);
    }

    [Fact]
    public void EnumTypedParameterWithNoRouteTokenMatchIsAQueryParameter()
    {
        WriteSource("Models/WidgetKind.cs", """
            namespace Test.Models
            {
                public enum WidgetKind { Small, Large }
            }
            """);
        WriteSource("Controllers/WidgetsController.cs", """
            using System.Web.Http;
            using Test.Models;

            namespace Test.Controllers
            {
                [RoutePrefix("api/widgets")]
                public class WidgetsController : ApiController
                {
                    [HttpGet]
                    [Route("search")]
                    public IHttpActionResult Search(WidgetKind? kind = null)
                    {
                        return Ok();
                    }
                }
            }
            """);

        var endpoint = Assert.Single(ReadWebApi2().Surface.Endpoints);
        var param = Assert.Single(endpoint.Parameters);
        Assert.Equal(ParameterLocation.Query, param.Location);
        Assert.False(param.Required);
        Assert.Equal("WidgetKind", param.SchemaType);
    }

    [Fact]
    public void ComplexTypeWithNoAttributeAndNoRouteTokenMatchBindsAsTheRequestBody()
    {
        WriteSource("Models/Widget.cs", """
            namespace Test.Models
            {
                public class Widget { public string Name { get; set; } }
            }
            """);
        WriteSource("Controllers/WidgetsController.cs", """
            using System.Web.Http;
            using Test.Models;

            namespace Test.Controllers
            {
                [RoutePrefix("api/widgets")]
                public class WidgetsController : ApiController
                {
                    [HttpPost]
                    [Route("")]
                    public IHttpActionResult Create(Widget widget)
                    {
                        return Ok(widget);
                    }
                }
            }
            """);

        var endpoint = Assert.Single(ReadWebApi2().Surface.Endpoints);
        Assert.Empty(endpoint.Parameters);
        Assert.NotNull(endpoint.RequestBody);
        Assert.Equal("Widget", endpoint.RequestBody!.SchemaName);
        Assert.True(endpoint.RequestBody.Required);
    }

    [Fact]
    public void FromBodyAttributeIsHonoredEvenForASimpleType()
    {
        WriteSource("Controllers/WidgetsController.cs", """
            using System.Web.Http;

            namespace Test.Controllers
            {
                [RoutePrefix("api/widgets")]
                public class WidgetsController : ApiController
                {
                    [HttpPost]
                    [Route("raw")]
                    public IHttpActionResult SetName([FromBody] string name)
                    {
                        return Ok();
                    }
                }
            }
            """);

        var endpoint = Assert.Single(ReadWebApi2().Surface.Endpoints);
        Assert.Empty(endpoint.Parameters);
        Assert.Equal("string", endpoint.RequestBody!.SchemaName);
    }

    [Fact]
    public void ResponseTypeAttributeDeclaresASchemaWithNoPinnedStatusCode()
    {
        WriteSource("Models/Widget.cs", """
            namespace Test.Models
            {
                public class Widget { }
            }
            """);
        WriteSource("Controllers/WidgetsController.cs", """
            using System.Web.Http;
            using System.Web.Http.Description;
            using Test.Models;

            namespace Test.Controllers
            {
                [RoutePrefix("api/widgets")]
                public class WidgetsController : ApiController
                {
                    [HttpGet]
                    [Route("{id}")]
                    [ResponseType(typeof(Widget))]
                    public IHttpActionResult GetWidget(string id)
                    {
                        return Ok(new Widget());
                    }
                }
            }
            """);

        var endpoint = Assert.Single(ReadWebApi2().Surface.Endpoints);
        Assert.Contains(endpoint.Responses, r => r.StatusCode == null && r.SchemaName == "Widget");
        Assert.Contains(endpoint.Responses, r => r.StatusCode == 200 && r.SchemaName == null);
    }

    [Fact]
    public void StatusCodeHelperWithAnEnumArgumentResolvesToItsNumericCode()
    {
        WriteSource("Controllers/WidgetsController.cs", """
            using System.Net;
            using System.Web.Http;

            namespace Test.Controllers
            {
                [RoutePrefix("api/widgets")]
                public class WidgetsController : ApiController
                {
                    [HttpDelete]
                    [Route("{id}")]
                    public IHttpActionResult Delete(string id)
                    {
                        return StatusCode(HttpStatusCode.NoContent);
                    }
                }
            }
            """);

        var endpoint = Assert.Single(ReadWebApi2().Surface.Endpoints);
        var response = Assert.Single(endpoint.Responses);
        Assert.Equal(204, response.StatusCode);
    }

    [Fact]
    public void AnUnrecognisedHelperWhoseFirstArgumentIsAStatusConstantStillResolves()
    {
        // "Problem" isn't a framework method the descriptor knows about, but its first
        // argument is read directly from the source — not inferred from what the
        // (possibly locally-defined) callee does with it.
        WriteSource("Controllers/WidgetsController.cs", """
            using System.Net;
            using System.Web.Http;

            namespace Test.Controllers
            {
                [RoutePrefix("api/widgets")]
                public class WidgetsController : ApiController
                {
                    [HttpGet]
                    [Route("{id}")]
                    public IHttpActionResult GetWidget(string id)
                    {
                        return Problem(HttpStatusCode.NotFound);
                    }

                    private IHttpActionResult Problem(HttpStatusCode code)
                    {
                        return ResponseMessage(Request.CreateResponse(code));
                    }
                }
            }
            """);

        var result = ReadWebApi2();
        var endpoint = Assert.Single(result.Surface.Endpoints);

        var response = Assert.Single(endpoint.Responses);
        Assert.Equal(404, response.StatusCode);
        Assert.False(endpoint.HasUnresolvedResponses);
    }

    [Fact]
    public void AResponseBuiltFromALocalVariableIsUndeterminedRatherThanGuessed()
    {
        WriteSource("Controllers/WidgetsController.cs", """
            using System.Net;
            using System.Web.Http;

            namespace Test.Controllers
            {
                [RoutePrefix("api/widgets")]
                public class WidgetsController : ApiController
                {
                    [HttpGet]
                    [Route("{id}")]
                    public IHttpActionResult GetWidget(string id)
                    {
                        var response = Request.CreateResponse(HttpStatusCode.NotFound);
                        return ResponseMessage(response);
                    }
                }
            }
            """);

        var result = ReadWebApi2();
        var endpoint = Assert.Single(result.Surface.Endpoints);

        Assert.Empty(endpoint.Responses);
        Assert.True(endpoint.HasUnresolvedResponses);
        Assert.Contains(result.Limitations, l => l.Contains("GetWidget") && l.Contains("status code could not be determined"));
    }

    [Fact]
    public void AnActionWithNoRouteAttributeIsExcludedAndReportedAsALimitation()
    {
        WriteSource("Controllers/WidgetsController.cs", """
            using System.Web.Http;

            namespace Test.Controllers
            {
                public class WidgetsController : ApiController
                {
                    [HttpGet]
                    public IHttpActionResult GetWidget()
                    {
                        return Ok();
                    }
                }
            }
            """);

        var result = ReadWebApi2();

        Assert.Empty(result.Surface.Endpoints);
        Assert.Contains(result.Limitations, l => l.Contains("GetWidget") && l.Contains("registered conventionally"));
    }

    [Fact]
    public void ATransitiveBaseControllerIsStillRecognised()
    {
        WriteSource("Controllers/BaseController.cs", """
            using System.Web.Http;

            namespace Test.Controllers
            {
                public abstract class BaseController : ApiController
                {
                }
            }
            """);
        WriteSource("Controllers/WidgetsController.cs", """
            using System.Web.Http;

            namespace Test.Controllers
            {
                [RoutePrefix("api/widgets")]
                public class WidgetsController : BaseController
                {
                    [HttpGet]
                    [Route("")]
                    public IHttpActionResult GetAll()
                    {
                        return Ok();
                    }
                }
            }
            """);

        Assert.Single(ReadWebApi2().Surface.Endpoints);
    }

    [Fact]
    public void AnEarlyNullOrEmptyGuardThatReturnsMakesAnOptionalParameterEffectivelyRequired()
    {
        WriteSource("Controllers/WidgetsController.cs", """
            using System.Web.Http;

            namespace Test.Controllers
            {
                [RoutePrefix("api/widgets")]
                public class WidgetsController : ApiController
                {
                    [HttpGet]
                    [Route("search")]
                    public IHttpActionResult Search(string tag = null)
                    {
                        if (string.IsNullOrWhiteSpace(tag))
                        {
                            return BadRequest();
                        }

                        return Ok();
                    }
                }
            }
            """);

        var endpoint = Assert.Single(ReadWebApi2().Surface.Endpoints);
        var param = Assert.Single(endpoint.Parameters);

        Assert.True(param.Required);
    }

    [Fact]
    public void ARangeCheckThatIsNotANullOrEmptyGuardLeavesAnOptionalParameterOptional()
    {
        WriteSource("Controllers/WidgetsController.cs", """
            using System.Web.Http;

            namespace Test.Controllers
            {
                [RoutePrefix("api/widgets")]
                public class WidgetsController : ApiController
                {
                    [HttpGet]
                    [Route("search")]
                    public IHttpActionResult Search(int page = 1)
                    {
                        if (page < 1)
                        {
                            return BadRequest();
                        }

                        return Ok();
                    }
                }
            }
            """);

        var endpoint = Assert.Single(ReadWebApi2().Surface.Endpoints);
        var param = Assert.Single(endpoint.Parameters);

        Assert.False(param.Required);
    }

    [Fact]
    public void APlainClassNotDerivedFromTheControllerBaseIsIgnored()
    {
        WriteSource("Services/WidgetService.cs", """
            namespace Test.Services
            {
                public class WidgetService
                {
                    public string GetWidget() => "not an action";
                }
            }
            """);

        Assert.Empty(ReadWebApi2().Surface.Endpoints);
    }
}
