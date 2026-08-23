namespace SpecDrift.Core.CodeReading;

/// <summary>
/// The project's dialect could not be inferred from its shape. A tool error (exit
/// code 2, per the CLI contract) — a failed detection must send the caller to
/// <c>--dialect</c> rather than have the tool silently guess and proceed.
/// </summary>
public sealed class DialectDetectionException : Exception
{
    public DialectDetectionException(string message) : base(message)
    {
    }
}
