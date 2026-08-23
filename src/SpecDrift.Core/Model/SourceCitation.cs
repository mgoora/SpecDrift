namespace SpecDrift.Core.Model;

/// <summary>
/// Where an <see cref="ApiSurface"/> element came from. Every finding cites both a
/// <see cref="SpecCitation"/> and a <see cref="CodeCitation"/> — a finding that cannot
/// is a bug, per the honesty rules.
/// </summary>
public abstract record SourceCitation
{
    public abstract string Describe();
}

/// <summary>A JSON Pointer into the OpenAPI document, e.g. <c>#/paths/~1api~1clients~1{id}/get</c>.</summary>
public sealed record SpecCitation(string JsonPointer) : SourceCitation
{
    public override string Describe() => JsonPointer;
}

/// <summary>A file path (relative to the analysed project) and line number.</summary>
public sealed record CodeCitation(string RelativeFilePath, int Line) : SourceCitation
{
    public override string Describe() => $"{RelativeFilePath}:{Line}";
}
