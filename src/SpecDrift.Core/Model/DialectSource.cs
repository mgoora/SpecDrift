namespace SpecDrift.Core.Model;

/// <summary>Whether the report's dialect came from <c>--dialect</c> or was inferred
/// from the project's shape. Shown in the report header — a wrong inference must be
/// visible, never silent, and the reader should be able to tell a guess from a
/// deliberate override at a glance.</summary>
public enum DialectSource
{
    Detected,
    Specified
}
