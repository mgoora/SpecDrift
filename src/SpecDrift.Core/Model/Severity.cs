namespace SpecDrift.Core.Model;

/// <summary>
/// A finding's severity. Ordinal order (Low &lt; Medium &lt; High) matters:
/// --min-severity compares against this ordering to decide the exit code.
/// </summary>
public enum Severity
{
    Low,
    Medium,
    High
}
