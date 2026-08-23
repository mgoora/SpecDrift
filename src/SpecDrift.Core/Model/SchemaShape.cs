namespace SpecDrift.Core.Model;

/// <summary>One property of a named schema/type — its describable type (see
/// <see cref="ApiResponse.SchemaName"/> for the naming convention) and where it's
/// declared, on whichever side this shape came from. <see cref="IsSchemaReference"/>
/// is true only when the property's type (or, for a list, its element type) is itself
/// a reference to another named schema/class — a plain OpenAPI primitive like
/// <c>string</c>/<c>format: uuid</c> has no equivalent-CLR-type table behind it, so
/// comparing it against C#'s <c>Guid</c> would be a false positive, not a finding;
/// only reference-typed properties are structurally comparable across both sides.</summary>
public sealed record SchemaProperty(string? TypeDescription, bool IsSchemaReference, SourceCitation Citation);

/// <summary>
/// A named schema's (or C# class's) shallow property shape. Built once per spec or
/// project, keyed by name, so the comparer can look a declared response type up by
/// name and compare its properties across both sides — needed because two schemas can
/// keep the same top-level name while a property's type quietly changes underneath it
/// (see SD009).
/// </summary>
public sealed record SchemaShape(string Name, IReadOnlyDictionary<string, SchemaProperty> Properties);
