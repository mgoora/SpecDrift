namespace SpecDrift.Core.SpecReading;

/// <summary>
/// The OpenAPI document could not be parsed. This is a tool error (exit code 2, per
/// the CLI contract) — not a drift finding, since there is nothing yet to compare.
/// </summary>
public sealed class SpecParseException : Exception
{
    public IReadOnlyList<string> Errors { get; }

    public SpecParseException(IReadOnlyList<string> errors)
        : base("Failed to parse the OpenAPI specification: " + string.Join("; ", errors))
    {
        Errors = errors;
    }
}
