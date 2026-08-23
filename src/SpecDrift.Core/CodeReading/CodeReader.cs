using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using SpecDrift.Core.Model;

namespace SpecDrift.Core.CodeReading;

/// <summary>
/// Stage 2 of the pipeline: one Roslyn walker, driven entirely by a
/// <see cref="DialectDescriptor"/>, producing the same <see cref="ApiSurface"/> shape
/// the spec reader produces. This is pure syntax analysis — no semantic model, no
/// compilation of the analysed project — matching "Roslyn reads any C# source as
/// text; the analysed project's target framework is irrelevant to the tool's own."
/// Two small cross-file syntax indexes (a class's declared base name, and which
/// simple names are enum declarations) stand in for the type resolution a full
/// compilation would otherwise provide.
/// </summary>
public static class CodeReader
{
    private static readonly HashSet<string> ClrSimpleTypeNames = new(StringComparer.Ordinal)
    {
        "string", "bool", "byte", "sbyte", "short", "ushort", "int", "uint", "long", "ulong",
        "float", "double", "decimal", "char", "Guid", "DateTime", "DateTimeOffset", "TimeSpan"
    };

    private static readonly (string Prefix, HttpVerb Verb)[] NamePrefixVerbs =
    [
        ("Get", HttpVerb.Get),
        ("Post", HttpVerb.Post),
        ("Put", HttpVerb.Put),
        ("Delete", HttpVerb.Delete),
        ("Patch", HttpVerb.Patch),
        ("Head", HttpVerb.Head),
        ("Options", HttpVerb.Options),
    ];

    private static readonly Regex RouteTokenPattern = new(@"\{([^:}]+)(?::[^}]+)?\}", RegexOptions.Compiled);

    public static CodeReadResult Read(string projectDirectory, DialectDescriptor descriptor)
    {
        var limitations = new List<string>();

        var parsedFiles = Directory.EnumerateFiles(projectDirectory, "*.cs", SearchOption.AllDirectories)
            .Where(path => !IsBuildOutputPath(path))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .Select(path => (Path: path, Root: CSharpSyntaxTree.ParseText(File.ReadAllText(path), path: path).GetCompilationUnitRoot()))
            .ToList();

        var baseClassByName = new Dictionary<string, string>(StringComparer.Ordinal);
        var knownEnumNames = new HashSet<string>(StringComparer.Ordinal);
        var typeShapes = new Dictionary<string, SchemaShape>(StringComparer.Ordinal);

        foreach (var (filePath, root) in parsedFiles)
        {
            var relativeFilePathForTypes = ToRelativePath(projectDirectory, filePath);

            foreach (var classDecl in root.DescendantNodes().OfType<ClassDeclarationSyntax>())
            {
                var baseName = classDecl.BaseList?.Types
                    .Select(t => SimpleTypeName(t.Type))
                    .FirstOrDefault();

                if (baseName is not null)
                {
                    baseClassByName[classDecl.Identifier.Text] = baseName;
                }

                var properties = ReadPropertyShapes(classDecl, relativeFilePathForTypes);
                if (properties.Count > 0)
                {
                    typeShapes[classDecl.Identifier.Text] = new SchemaShape(classDecl.Identifier.Text, properties);
                }
            }

            foreach (var enumDecl in root.DescendantNodes().OfType<EnumDeclarationSyntax>())
            {
                knownEnumNames.Add(enumDecl.Identifier.Text);
            }
        }

        bool DerivesFromControllerBase(string className)
        {
            var current = className;
            var visited = new HashSet<string>(StringComparer.Ordinal);

            while (visited.Add(current))
            {
                if (descriptor.ControllerBaseClassNames.Contains(current))
                {
                    return true;
                }

                if (!baseClassByName.TryGetValue(current, out var parent))
                {
                    return false;
                }

                current = parent;
            }

            return false;
        }

        var endpoints = new List<ApiEndpoint>();

        foreach (var (filePath, root) in parsedFiles)
        {
            var relativeFilePath = ToRelativePath(projectDirectory, filePath);

            foreach (var classDecl in root.DescendantNodes().OfType<ClassDeclarationSyntax>())
            {
                if (!DerivesFromControllerBase(classDecl.Identifier.Text))
                {
                    continue;
                }

                var routePrefix = descriptor.RoutePrefixAttributeName is not null
                    ? GetStringAttributeArgument(classDecl.AttributeLists, descriptor.RoutePrefixAttributeName) ?? string.Empty
                    : string.Empty;

                foreach (var method in classDecl.Members.OfType<MethodDeclarationSyntax>())
                {
                    if (!method.Modifiers.Any(SyntaxKind.PublicKeyword))
                    {
                        continue;
                    }

                    var verbMatch = ResolveVerbAndAttribute(method, descriptor);
                    if (verbMatch is null)
                    {
                        continue;
                    }

                    var (verb, verbAttribute) = verbMatch.Value;

                    // The action's own route segment can come from the matched HTTP-verb
                    // attribute's own argument (ASP.NET Core's [HttpGet("{id}")]) or from
                    // a separate attribute named by RouteAttributeName (Web API 2's
                    // [Route("{id}")], and Core's bare [Route] used the same way).
                    var actionRoute = GetStringAttributeArgument(verbAttribute)
                        ?? GetStringAttributeArgument(method.AttributeLists, descriptor.RouteAttributeName);

                    if (actionRoute is null)
                    {
                        // An explicit HTTP-verb attribute establishes attribute routing on
                        // its own in ASP.NET Core even with no argument — the action's own
                        // segment is simply empty (it inherits the controller's route).
                        // Web API 2's verb attributes never do this on their own; without a
                        // companion [Route], the action is convention-routed and genuinely
                        // unknown.
                        if (verbAttribute is not null && descriptor.HttpMethodAttributesEstablishRouting)
                        {
                            actionRoute = string.Empty;
                        }
                        else
                        {
                            limitations.Add(
                                $"{relativeFilePath}:{LineOf(method)}: '{classDecl.Identifier.Text}.{method.Identifier.Text}' " +
                                $"has no [{descriptor.RouteAttributeName}] attribute — its route is likely registered " +
                                "conventionally and cannot be determined statically.");
                            continue;
                        }
                    }

                    var routeTemplate = CombineRoute(routePrefix, actionRoute);
                    if (descriptor.ExpandsControllerAndActionRouteTokens)
                    {
                        routeTemplate = ExpandRouteTokens(
                            routeTemplate, classDecl.Identifier.Text, method.Identifier.Text, descriptor.ControllerNameSuffixToStrip);
                    }

                    var routeTokenNames = ExtractRouteTokenNames(routeTemplate);

                    var (parameters, requestBody) = ReadParameters(
                        method, descriptor, routeTokenNames, knownEnumNames, relativeFilePath,
                        classDecl.Identifier.Text, limitations);

                    var (responses, hasUnresolvedResponses) = ReadResponses(
                        method, descriptor, relativeFilePath, classDecl.Identifier.Text, limitations);

                    endpoints.Add(new ApiEndpoint(
                        routeTemplate,
                        verb,
                        new CodeCitation(relativeFilePath, LineOf(method)),
                        parameters,
                        requestBody,
                        responses,
                        method.Identifier.Text,
                        hasUnresolvedResponses,
                        CodeSourceText: method.ToFullString().Trim()));
                }
            }
        }

        return new CodeReadResult(new ApiSurface(endpoints), typeShapes, limitations);
    }

    private static Dictionary<string, SchemaProperty> ReadPropertyShapes(ClassDeclarationSyntax classDecl, string relativeFilePath)
    {
        var properties = new Dictionary<string, SchemaProperty>(StringComparer.OrdinalIgnoreCase);

        foreach (var property in classDecl.Members.OfType<PropertyDeclarationSyntax>())
        {
            if (!property.Modifiers.Any(SyntaxKind.PublicKeyword))
            {
                continue;
            }

            var typeDescription = DescribePropertyType(property.Type);

            properties[property.Identifier.Text] = new SchemaProperty(
                typeDescription,
                IsReferenceTypeDescription(typeDescription),
                new CodeCitation(relativeFilePath, LineOf(property)));
        }

        return properties;
    }

    /// <summary>True when the (possibly array-wrapped) type name isn't a CLR
    /// primitive — i.e. it's a class or enum this project declares, the code-side
    /// equivalent of the spec side's "is this a <c>$ref</c>?" test.</summary>
    private static bool IsReferenceTypeDescription(string? typeDescription)
    {
        if (typeDescription is null)
        {
            return false;
        }

        var name = typeDescription.StartsWith("array<", StringComparison.Ordinal) && typeDescription.EndsWith('>')
            ? typeDescription[6..^1]
            : typeDescription;

        return !ClrSimpleTypeNames.Contains(name);
    }

    private static readonly HashSet<string> ListLikeGenericNames = new(StringComparer.Ordinal)
    {
        "List", "IList", "IEnumerable", "ICollection", "IReadOnlyList", "IReadOnlyCollection"
    };

    private static string? DescribePropertyType(TypeSyntax type)
    {
        if (type is NullableTypeSyntax nullable)
        {
            return DescribePropertyType(nullable.ElementType);
        }

        if (type is ArrayTypeSyntax arrayType)
        {
            return $"array<{SimpleTypeName(arrayType.ElementType)}>";
        }

        if (type is GenericNameSyntax generic
            && ListLikeGenericNames.Contains(generic.Identifier.Text)
            && generic.TypeArgumentList.Arguments.Count == 1)
        {
            return $"array<{SimpleTypeName(generic.TypeArgumentList.Arguments[0])}>";
        }

        return SimpleTypeName(type);
    }

    private static (List<ApiParameter> Parameters, RequestBodyShape? RequestBody) ReadParameters(
        MethodDeclarationSyntax method,
        DialectDescriptor descriptor,
        HashSet<string> routeTokenNames,
        HashSet<string> knownEnumNames,
        string relativeFilePath,
        string className,
        List<string> limitations)
    {
        var parameters = new List<ApiParameter>();
        RequestBodyShape? requestBody = null;

        foreach (var parameter in method.ParameterList.Parameters)
        {
            var name = parameter.Identifier.Text;

            // A parameter can carry a C# default (syntactically optional) while the
            // method body still rejects a request that omits it — e.g. a leading
            // `if (string.IsNullOrWhiteSpace(x)) return BadRequest(...);` guard. That
            // runtime behaviour is what "required" actually means to a caller, so it
            // wins over the parameter list's own optionality.
            var required = parameter.Default is null || HasRequiredGuardClause(method, name);
            var citation = new CodeCitation(relativeFilePath, LineOf(parameter));
            var typeName = SimpleTypeName(parameter.Type!);

            var hasBodyAttribute = HasAttribute(parameter.AttributeLists, descriptor.RequestBodyBindingAttributeName);
            var hasQueryAttribute = descriptor.QueryBindingAttributeName is not null
                && HasAttribute(parameter.AttributeLists, descriptor.QueryBindingAttributeName);

            if (hasBodyAttribute)
            {
                requestBody = new RequestBodyShape(typeName, required, citation);
                continue;
            }

            if (routeTokenNames.Contains(name))
            {
                parameters.Add(new ApiParameter(name, ParameterLocation.Path, true, typeName, citation));
                continue;
            }

            var isSimpleType = ClrSimpleTypeNames.Contains(typeName) || knownEnumNames.Contains(typeName);

            if (hasQueryAttribute || isSimpleType)
            {
                parameters.Add(new ApiParameter(name, ParameterLocation.Query, required, typeName, citation));
                continue;
            }

            if (requestBody is not null)
            {
                limitations.Add(
                    $"{relativeFilePath}:{LineOf(parameter)}: '{className}.{method.Identifier.Text}' has more than " +
                    $"one body-shaped parameter ('{name}') — only the first is recorded as its request body.");
                continue;
            }

            requestBody = new RequestBodyShape(typeName, required, citation);
        }

        return (parameters, requestBody);
    }

    private static (List<ApiResponse> Responses, bool HasUnresolved) ReadResponses(
        MethodDeclarationSyntax method,
        DialectDescriptor descriptor,
        string relativeFilePath,
        string className,
        List<string> limitations)
    {
        var responses = new List<ApiResponse>();
        var hasUnresolved = false;

        // Web API 2's [ResponseType(typeof(T))] always has exactly one argument and
        // never pins a status code. ASP.NET Core's [ProducesResponseType(...)] can
        // appear more than once per action, each pairing a type with its own status
        // code — typeof(T) plus an optional second (status) argument, or a bare status
        // argument with no type at all (e.g. [ProducesResponseType(404)]). One rule
        // covers both: a leading typeof(...) is the schema, anything else is read as a
        // status code directly.
        foreach (var attribute in FindAttributes(method.AttributeLists, descriptor.ResponseTypeAttributeName))
        {
            var arguments = attribute.ArgumentList?.Arguments;
            if (arguments is not { Count: > 0 })
            {
                continue;
            }

            var citation = new CodeCitation(relativeFilePath, LineOf(attribute));

            if (arguments.Value[0].Expression is TypeOfExpressionSyntax typeofExpr)
            {
                var pinnedCode = arguments.Value.Count > 1
                    ? ResolveStatusCodeExpression(arguments.Value[1].Expression, descriptor.StatusCodeConstants)
                    : null;

                responses.Add(new ApiResponse(pinnedCode, SimpleTypeName(typeofExpr.Type), citation));
            }
            else if (ResolveStatusCodeExpression(arguments.Value[0].Expression, descriptor.StatusCodeConstants) is { } statusOnly)
            {
                responses.Add(new ApiResponse(statusOnly, SchemaName: null, citation));
            }
        }

        // An expression-bodied method (`=> Ok();`) returns exactly as much as a
        // block-bodied one with a single `return` statement — just without a
        // ReturnStatementSyntax node to find. Treat both the same way.
        IEnumerable<(SyntaxNode Site, ExpressionSyntax Expression)> returnedExpressions = method.ExpressionBody is not null
            ? new[] { ((SyntaxNode)method.ExpressionBody, method.ExpressionBody.Expression) }
            : method.DescendantNodes().OfType<ReturnStatementSyntax>()
                .Where(r => r.Expression is not null)
                .Select(r => ((SyntaxNode)r, r.Expression!));

        foreach (var (site, expression) in returnedExpressions)
        {
            if (expression is InvocationExpressionSyntax invocation)
            {
                var calledName = SimpleInvocationName(invocation);

                int? resolvedCode = calledName is not null
                    && descriptor.StatusHelperMethods.TryGetValue(calledName, out var fixedCode)
                    && fixedCode is not null
                        ? fixedCode
                        : null;

                // Either a framework helper whose code depends on its argument
                // (StatusCode(...)), or a call the descriptor doesn't recognise at all
                // (e.g. a locally-defined Problem(...) helper) — either way, a
                // status-constant or numeric-literal argument is read directly from the
                // source, not inferred from what the callee does with it. A named
                // argument matching the descriptor's convention (Core's
                // `statusCode: ...`) is preferred over positional when present.
                resolvedCode ??= ResolveArgumentStatusCode(invocation, descriptor.StatusCodeConstants, descriptor.StatusCodeNamedArgumentName);

                if (resolvedCode is not null)
                {
                    responses.Add(new ApiResponse(
                        resolvedCode,
                        SchemaName: null,
                        new CodeCitation(relativeFilePath, LineOf(site))));
                    continue;
                }
            }

            // Known undeterminable case: an HttpResponseMessage (or similar) assembled
            // from a value that isn't itself present at the return site — e.g. a local
            // variable built up over several earlier statements.
            hasUnresolved = true;
            limitations.Add(
                $"{relativeFilePath}:{LineOf(site)}: '{className}.{method.Identifier.Text}' returns a " +
                "result whose status code could not be determined statically.");
        }

        return (responses, hasUnresolved);
    }

    /// <summary>True when the method contains an early guard — <c>if
    /// (string.IsNullOrWhiteSpace(x))</c> / <c>IsNullOrEmpty(x)</c> / <c>x == null</c>
    /// / <c>null == x</c> — on this exact parameter whose body returns. A syntactic,
    /// no-data-flow pattern match: it only recognises the condition directly testing
    /// the parameter, not one derived from it a few statements later.</summary>
    private static bool HasRequiredGuardClause(MethodDeclarationSyntax method, string parameterName) =>
        method.DescendantNodes()
            .OfType<IfStatementSyntax>()
            .Any(ifStatement =>
                ChecksParameterIsNullOrEmpty(ifStatement.Condition, parameterName)
                && ifStatement.Statement.DescendantNodesAndSelf().OfType<ReturnStatementSyntax>().Any());

    private static bool ChecksParameterIsNullOrEmpty(ExpressionSyntax condition, string parameterName)
    {
        if (condition is InvocationExpressionSyntax
            {
                Expression: MemberAccessExpressionSyntax { Name.Identifier.Text: "IsNullOrWhiteSpace" or "IsNullOrEmpty" },
                ArgumentList.Arguments: [{ Expression: IdentifierNameSyntax argument }]
            } && argument.Identifier.Text == parameterName)
        {
            return true;
        }

        if (condition is BinaryExpressionSyntax binary && binary.IsKind(SyntaxKind.EqualsExpression))
        {
            var leftIsParam = binary.Left is IdentifierNameSyntax left && left.Identifier.Text == parameterName;
            var rightIsParam = binary.Right is IdentifierNameSyntax right && right.Identifier.Text == parameterName;
            var leftIsNull = binary.Left.IsKind(SyntaxKind.NullLiteralExpression);
            var rightIsNull = binary.Right.IsKind(SyntaxKind.NullLiteralExpression);

            return (leftIsParam && rightIsNull) || (rightIsParam && leftIsNull);
        }

        return false;
    }

    /// <summary>Resolves the action's HTTP verb, and — when the verb came from a
    /// matching attribute — that attribute itself, so the caller can also check it
    /// for an inline route argument (ASP.NET Core's <c>[HttpGet("{id}")]</c>).</summary>
    private static (HttpVerb Verb, AttributeSyntax? Attribute)? ResolveVerbAndAttribute(
        MethodDeclarationSyntax method, DialectDescriptor descriptor)
    {
        foreach (var attribute in method.AttributeLists.SelectMany(list => list.Attributes))
        {
            if (descriptor.HttpMethodAttributeNames.TryGetValue(SimpleName(attribute.Name), out var verb))
            {
                return (verb, attribute);
            }
        }

        if (!descriptor.InfersHttpMethodFromNamePrefix)
        {
            return null;
        }

        var methodName = method.Identifier.Text;
        foreach (var (prefix, verb) in NamePrefixVerbs)
        {
            if (methodName.StartsWith(prefix, StringComparison.Ordinal))
            {
                return (verb, null);
            }
        }

        return null;
    }

    /// <summary>Replaces ASP.NET Core's <c>[controller]</c>/<c>[action]</c> route
    /// tokens with the controller's name (minus its conventional suffix) and the
    /// action method's name. A no-op call when the descriptor doesn't use them
    /// (<see cref="DialectDescriptor.ExpandsControllerAndActionRouteTokens"/> gates
    /// whether this is ever called at all).</summary>
    private static string ExpandRouteTokens(string routeTemplate, string className, string methodName, string? suffixToStrip)
    {
        var controllerName = suffixToStrip is not null && className.EndsWith(suffixToStrip, StringComparison.Ordinal)
            ? className[..^suffixToStrip.Length]
            : className;

        return routeTemplate
            .Replace("[controller]", controllerName, StringComparison.OrdinalIgnoreCase)
            .Replace("[action]", methodName, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Resolves the status-code-carrying argument of a call — preferring a
    /// named argument matching <paramref name="namedArgumentName"/> (Core's
    /// <c>statusCode: ...</c> convention) when one is given, falling back to the
    /// first positional argument otherwise (Web API 2 never names this argument).</summary>
    private static int? ResolveArgumentStatusCode(
        InvocationExpressionSyntax invocation, IReadOnlyDictionary<string, int> statusCodeConstants, string? namedArgumentName)
    {
        var arguments = invocation.ArgumentList.Arguments;

        var candidate = namedArgumentName is not null
            ? arguments.FirstOrDefault(a => a.NameColon?.Name.Identifier.Text == namedArgumentName)?.Expression
            : null;

        candidate ??= arguments.FirstOrDefault()?.Expression;

        return ResolveStatusCodeExpression(candidate, statusCodeConstants);
    }

    private static int? ResolveStatusCodeExpression(ExpressionSyntax? expression, IReadOnlyDictionary<string, int> statusCodeConstants) =>
        expression switch
        {
            LiteralExpressionSyntax literal when literal.IsKind(SyntaxKind.NumericLiteralExpression)
                && literal.Token.Value is int numericValue => numericValue,
            MemberAccessExpressionSyntax memberAccess
                when statusCodeConstants.TryGetValue(memberAccess.Name.Identifier.Text, out var code) => code,
            _ => null
        };

    private static string? SimpleInvocationName(InvocationExpressionSyntax invocation) => invocation.Expression switch
    {
        IdentifierNameSyntax identifier => identifier.Identifier.Text,
        MemberAccessExpressionSyntax { Expression: ThisExpressionSyntax } memberAccess => memberAccess.Name.Identifier.Text,
        _ => null
    };

    private static string SimpleName(NameSyntax name) => name switch
    {
        QualifiedNameSyntax qualified => qualified.Right.Identifier.Text,
        SimpleNameSyntax simple => simple.Identifier.Text,
        _ => name.ToString()
    };

    private static bool HasAttribute(SyntaxList<AttributeListSyntax> attributeLists, string attributeName) =>
        FindAttribute(attributeLists, attributeName) is not null;

    private static AttributeSyntax? FindAttribute(SyntaxList<AttributeListSyntax> attributeLists, string attributeName) =>
        attributeLists.SelectMany(list => list.Attributes).FirstOrDefault(a => SimpleName(a.Name) == attributeName);

    private static IEnumerable<AttributeSyntax> FindAttributes(SyntaxList<AttributeListSyntax> attributeLists, string attributeName) =>
        attributeLists.SelectMany(list => list.Attributes).Where(a => SimpleName(a.Name) == attributeName);

    private static string? GetStringAttributeArgument(SyntaxList<AttributeListSyntax> attributeLists, string attributeName) =>
        GetStringAttributeArgument(FindAttribute(attributeLists, attributeName));

    private static string? GetStringAttributeArgument(AttributeSyntax? attribute)
    {
        var literal = attribute?.ArgumentList?.Arguments.FirstOrDefault()?.Expression as LiteralExpressionSyntax;
        return literal?.Token.Value as string;
    }

    private static string SimpleTypeName(TypeSyntax type) => type switch
    {
        NullableTypeSyntax nullable => SimpleTypeName(nullable.ElementType),
        QualifiedNameSyntax qualified => qualified.Right.Identifier.Text,
        _ => type.ToString()
    };

    private static HashSet<string> ExtractRouteTokenNames(string routeTemplate)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in RouteTokenPattern.Matches(routeTemplate))
        {
            names.Add(match.Groups[1].Value);
        }

        return names;
    }

    private static string CombineRoute(string prefix, string actionRoute)
    {
        var trimmedPrefix = prefix.Trim('/');
        var trimmedAction = actionRoute.Trim('/');

        return (trimmedPrefix.Length, trimmedAction.Length) switch
        {
            (0, 0) => string.Empty,
            (0, _) => trimmedAction,
            (_, 0) => trimmedPrefix,
            _ => $"{trimmedPrefix}/{trimmedAction}"
        };
    }

    private static int LineOf(SyntaxNode node) => node.GetLocation().GetLineSpan().StartLinePosition.Line + 1;

    private static string ToRelativePath(string projectDirectory, string filePath) =>
        Path.GetRelativePath(projectDirectory, filePath).Replace('\\', '/');

    private static bool IsBuildOutputPath(string path)
    {
        var segments = path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return segments.Contains("bin", StringComparer.OrdinalIgnoreCase)
            || segments.Contains("obj", StringComparer.OrdinalIgnoreCase);
    }
}
