using SpecDrift.Core.Model;

namespace SpecDrift.Core.SpecReading;

/// <summary>The spec reader's output: the extracted <see cref="ApiSurface"/>, the
/// document's declared version (for the report header), and every named component
/// schema's shallow property shape — needed to compare a declared response type's
/// properties against the code's, not just its name (see SD009).</summary>
public sealed record SpecReadResult(
    ApiSurface Surface,
    string SpecVersion,
    IReadOnlyDictionary<string, SchemaShape> Schemas);
