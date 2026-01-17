namespace Jmodot.Implementation.AI.Affinities;

using System;
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
    /// Fired when an affinity value changes. Provides the affinity, old value, and new value.
    /// </summary>
    public event Action<Affinity, float, float> AffinityChanged = delegate { };

    /// <summary>
    ///     Gets the current value of a specific affinity. Returns null if the affinity is not defined for this agent.
    /// </summary>
    public float? GetAffinity(Affinity affinity)
    {
        if (this._affinities.TryGetValue(affinity, out float value))
        {
            return value;
        }

        return null;
    }

    /// <summary>
    /// Tries to get an affinity value. Returns true if the affinity exists.
    /// </summary>
    public bool TryGetAffinity(Affinity affinity, out float value)
    {
        return this._affinities.TryGetValue(affinity, out value);
    }

    /// <summary>
    ///     Sets the value of an affinity at runtime (e.g., an effect could temporarily increase Fear).
    ///     Fires AffinityChanged event if the value actually changes.
    /// </summary>
    public void SetAffinity(Affinity affinity, float value)
    {
        float oldValue = this._affinities.TryGetValue(affinity, out float existing) ? existing : 0f;
        float newValue = Mathf.Clamp(value, 0f, 1f);

        // Always store the value (ensures GetAffinity returns a value, not null)
        this._affinities[affinity] = newValue;

        // Only fire event if there was a meaningful change (epsilon comparison for floats)
        if (Mathf.Abs(oldValue - newValue) >= 0.001f)
        {
            this.AffinityChanged.Invoke(affinity, oldValue, newValue);
        }
    }

    /// <summary>
    /// Modifies an affinity by a delta value. Useful for incremental changes.
    /// Creates the affinity with delta as value if it doesn't exist (treating missing as 0).
    /// </summary>
    /// <param name="affinity">The affinity to modify.</param>
    /// <param name="delta">The amount to add (can be negative).</param>
    public void ModifyAffinity(Affinity affinity, float delta)
    {
        float current = this.GetAffinity(affinity) ?? 0f;
        this.SetAffinity(affinity, current + delta);
    }
}
