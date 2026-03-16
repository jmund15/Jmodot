namespace Jmodot.Core.AI.Perception;

/// <summary>
/// Defines how multiple sensor contributions are combined into a single fused confidence value.
/// </summary>
public enum FusionMode
{
    /// <summary>Sum of all contributions, clamped to [0, 1]. Default — senses complement each other.</summary>
    Additive,

    /// <summary>Best single contribution wins. Conservative — no saturation risk.</summary>
    Max
}
