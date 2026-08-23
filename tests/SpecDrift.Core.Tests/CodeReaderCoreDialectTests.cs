using SpecDrift.Core.CodeReading;
using SpecDrift.Core.Model;

namespace SpecDrift.Core.Tests;

/// <summary>
/// The same walker as <see cref="CodeReaderTests"/>, driven by
/// <see cref="DialectDescriptors.Core"/> instead — proving the dialect rule: the
/// differences below are all descriptor data, not a second walker.
/// </summary>
public class CodeReaderCoreDialectTests : IDisposable
{
    private readonly string _projectDir;

    public CodeReaderCoreDialectTests()
    {
        _projectDir = Path.Combine(Path.GetTempPath(), "specdrift-code-reader-core-tests-" + Guid.NewGuid());
        Directory.CreateDirectory(_projectDir);
    }

    public void Dispose() => Directory.Delete(_projectDir, recursive: true);

    private void WriteSource(string relativePath, string content)
    {
        var fullPath = Path.Combine(_projectDir, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, content);
    }

    private CodeReadResult ReadCore() => CodeReader.Read(_projectDir, DialectDescriptors.Core);

    [Fact]
    public void AnInlineRouteOnTheHttpVerbAttributeIsUsedDirectly()
    {
        WriteSource("Controllers/WidgetsController.cs", """
            using Microsoft.AspNetCore.Mvc;

            namespace Test.Controllers
            {
                [ApiController]
                [Route("api/widgets")]
                public class WidgetsController : ControllerBase
                {
                    [HttpGet("{id}")]
                    public IActionResult GetWidget(string id) => Ok(id);
                }
            }
            """);

        var endpoint = Assert.Single(ReadCore().Surface.Endpoints);
        Assert.Equal("api/widgets/{id}", endpoint.RouteTemplate);
        Assert.Equal(HttpVerb.Get, endpoint.Method);
    }

    [Fact]
    public void ABareHttpVerbAttributeWithNoArgumentInheritsJustTheControllerPrefix()
    {
        // No [Route] at all on the action — unlike Web API 2, an explicit ASP.NET Core
        // verb attribute establishes attribute routing on its own.
        WriteSource("Controllers/WidgetsController.cs", """
            using Microsoft.AspNetCore.Mvc;

            namespace Test.Controllers
            {
                [ApiController]
                [Route("api/widgets")]
                public class WidgetsController : ControllerBase
                {
                    [HttpPost]
                    public IActionResult Create() => Ok();
                }
            }
            """);

        var endpoint = Assert.Single(ReadCore().Surface.Endpoints);
        Assert.Equal("api/widgets", endpoint.RouteTemplate);
        Assert.Equal(HttpVerb.Post, endpoint.Method);
    }

    [Fact]
    public void ControllerAndActionRouteTokensAreExpanded()
    {
        WriteSource("Controllers/WidgetsController.cs", """
            using Microsoft.AspNetCore.Mvc;

            namespace Test.Controllers
            {
                [ApiController]
                [Route("api/[controller]/[action]")]
                public class WidgetsController : ControllerBase
                {
                    [HttpGet]
                    public IActionResult ListAll() => Ok();
                }
            }
            """);

        var endpoint = Assert.Single(ReadCore().Surface.Endpoints);
        Assert.Equal("api/Widgets/ListAll", endpoint.RouteTemplate);
    }

    [Fact]
    public void ProducesResponseTypePairsATypeWithItsOwnPinnedStatusCode()
    {
        WriteSource("Models/Widget.cs", """
            namespace Test.Models { public class Widget { } }
            """);
        WriteSource("Controllers/WidgetsController.cs", """
            using Microsoft.AspNetCore.Mvc;
            using Test.Models;

            namespace Test.Controllers
            {
                [ApiController]
                [Route("api/widgets")]
                public class WidgetsController : ControllerBase
                {
                    [HttpGet("{id}")]
                    [ProducesResponseType(typeof(Widget), StatusCodes.Status200OK)]
                    [ProducesResponseType(StatusCodes.Status404NotFound)]
                    public ActionResult<Widget> GetWidget(string id) => Ok(new Widget());
                }
            }
            """);

        var endpoint = Assert.Single(ReadCore().Surface.Endpoints);

        Assert.Contains(endpoint.Responses, r => r.StatusCode == 200 && r.SchemaName == "Widget");
        Assert.Contains(endpoint.Responses, r => r.StatusCode == 404 && r.SchemaName == null);
    }

    [Fact]
    public void FromQueryAttributeBindsAsAQueryParameter()
    {
        WriteSource("Controllers/WidgetsController.cs", """
            using Microsoft.AspNetCore.Mvc;

            namespace Test.Controllers
            {
                [ApiController]
                [Route("api/widgets")]
                public class WidgetsController : ControllerBase
                {
                    [HttpGet("search")]
                    public IActionResult Search([FromQuery] string tag) => Ok();
                }
            }
            """);

        var endpoint = Assert.Single(ReadCore().Surface.Endpoints);
        var param = Assert.Single(endpoint.Parameters);
        Assert.Equal(ParameterLocation.Query, param.Location);
    }

    [Fact]
    public void ProblemsNamedStatusCodeArgumentResolvesRegardlessOfArgumentOrder()
    {
        WriteSource("Controllers/WidgetsController.cs", """
            using Microsoft.AspNetCore.Mvc;

            namespace Test.Controllers
            {
                [ApiController]
                [Route("api/widgets")]
                public class WidgetsController : ControllerBase
                {
                    [HttpGet("{id}")]
                    public IActionResult GetWidget(string id) =>
                        Problem(title: "Not found", statusCode: StatusCodes.Status404NotFound);
                }
            }
            """);

        var endpoint = Assert.Single(ReadCore().Surface.Endpoints);
        var response = Assert.Single(endpoint.Responses);
        Assert.Equal(404, response.StatusCode);
        Assert.False(endpoint.HasUnresolvedResponses);
    }

    [Fact]
    public void CreatedAtActionResolvesToCreated()
    {
        WriteSource("Controllers/WidgetsController.cs", """
            using Microsoft.AspNetCore.Mvc;

            namespace Test.Controllers
            {
                [ApiController]
                [Route("api/widgets")]
                public class WidgetsController : ControllerBase
                {
                    [HttpPost]
                    public IActionResult Create() => CreatedAtAction(nameof(GetWidget), new { id = 1 }, null);

                    [HttpGet("{id}")]
                    public IActionResult GetWidget(int id) => Ok();
                }
            }
            """);

        var endpoint = ReadCore().Surface.Endpoints.Single(e => e.Method == HttpVerb.Post);
        var response = Assert.Single(endpoint.Responses);
        Assert.Equal(201, response.StatusCode);
    }

    [Fact]
    public void TheAlternateControllerBaseClassNameIsAlsoRecognised()
    {
        // Core has two conventional base classes — ControllerBase (API-only) and
        // Controller (adds view support) — both are recognised, not just the first.
        WriteSource("Controllers/WidgetsController.cs", """
            using Microsoft.AspNetCore.Mvc;

            namespace Test.Controllers
            {
                [ApiController]
                [Route("api/widgets")]
                public class WidgetsController : Controller
                {
                    [HttpGet]
                    public IActionResult ListAll() => Ok();
                }
            }
            """);

        Assert.Single(ReadCore().Surface.Endpoints);
    }
}
