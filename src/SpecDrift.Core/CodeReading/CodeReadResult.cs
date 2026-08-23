using SpecDrift.Core.Model;

namespace SpecDrift.Core.CodeReading;

/// <summary>
/// The code reader's output: the extracted <see cref="ApiSurface"/>, every named
/// class's shallow property shape (for comparing a declared response type's
/// properties against the spec's — see SD009), and every construct it deliberately
/// could not resolve statically — conventionally registered routes, response bodies
/// assembled elsewhere, and the like. Nothing is dropped silently; per the honesty
/// rules, these belong in the report's own declared blind spots.
/// </summary>
public sealed record CodeReadResult(
    ApiSurface Surface,
    IReadOnlyDictionary<string, SchemaShape> Types,
    IReadOnlyList<string> Limitations);
