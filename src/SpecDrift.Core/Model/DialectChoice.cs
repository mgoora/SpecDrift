namespace SpecDrift.Core.Model;

/// <summary>
/// The dialect the code reader targets. <c>--dialect</c> selects one explicitly;
/// otherwise it is inferred from the analysed project's shape.
/// </summary>
public enum DialectChoice
{
    WebApi2,
    Core
}
