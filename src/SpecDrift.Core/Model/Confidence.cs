namespace SpecDrift.Core.Model;

/// <summary>Deterministic findings (stages 1-3) are Certain. LLM findings (stage 4,
/// SD100) are Probable — never the reverse, however confident the model sounds.</summary>
public enum Confidence
{
    Certain,
    Probable
}
