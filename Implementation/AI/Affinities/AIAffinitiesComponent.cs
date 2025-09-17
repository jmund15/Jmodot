namespace Jmodot.Implementation.AI.Affinities;

using Core.AI.Affinities;
using Godot.Collections;

/// <summary>
///     The definitive "personality" component for an AI. It holds the runtime values for
///     all of the AI's core personality traits, using Affinity resources as type-safe keys.
///     Both high-level (Utility AI) and low-level (Steering) systems read from this single
///     source of truth to inform their decisions.
/// </summary>
[GlobalClass]
public partial class AIAffinitiesComponent : Node
{
    [Export] private Dictionary<Affinity, float> _affinities = new();

    /// <summary>
    ///     Gets the current value of a specific affinity. Returns null (or 0?) if the affinity is not defined for this agent.
    /// </summary>
    public float? GetAffinity(Affinity affinity)
    {
        if (this._affinities.TryGetValue(affinity, out float value)) return value;
        return null;
        //return _affinities.GetValueOrDefault(affinity, 0.0f);
    }

    /// <summary>
    /// </summary>
    /// <param name="affinity">
    ///     <param name="value">
    ///         <returns></returns>
    public bool TryGetAffinity(Affinity affinity, out float value)
    {
        return this._affinities.TryGetValue(affinity, out value);
    }

    /// <summary>
    ///     Sets the value of an affinity at runtime (e.g., an effect could temporarily increase Fear).
    /// </summary>
    public void SetAffinity(Affinity affinity, float value)
    {
        this._affinities[affinity] = Mathf.Clamp(value, 0f, 1f);
        // Optional: Emit a signal here if other systems need to react to affinity changes in real-time.
    }
}
