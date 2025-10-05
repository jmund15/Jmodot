namespace Jmodot.Core.AI.Perception;

using Identification;
using Implementation.AI.Perception.Strategies;

/// <summary>
///     An immutable struct representing a single, stateless sensory event or "snapshot" in time.
///     It contains all context about a sensation at the moment it occurred. Sensors produce these
///     to be processed by the AIPerceptionManager.
/// </summary>
public readonly struct Percept3D
{
    // TODO: look at replacing Node3D with something more generic. Is identity enough info?
    // Currently there's no way to get the indentity's entity, it's just its label and categories.
    // That could be a valuable extension that allows us to get rid of 'Target'
    /// <summary>The entity that was perceived. Can be null for location-only percepts like sounds.</summary>
    public readonly Node3D? Target;

    /// <summary>The position where the sensation occurred or the last known position of the target.</summary>
    public readonly Vector3 LastKnownPosition;

    /// <summary>The velocity of the target at the moment of perception. For static objects, this will be Vector3.Zero.</summary>
    public readonly Vector3 LastKnownVelocity;

    /// <summary>The data-driven Identity of this percept (e.g., "Enemy.tres").</summary>
    public readonly Identity Identity = null!;

    /// <summary>The strength of the sensation, from 0.0 (barely perceived) to 1.0 (clearly perceived).</summary>
    public readonly float Confidence;

    /// <summary>The strategy defining how this memory should fade over time.</summary>
    public readonly MemoryDecayStrategy DecayStrategy = null!;

    /// <summary>The timestamp (in milliseconds via Time.GetTicksMsec()) when this percept was generated.</summary>
    public readonly ulong Timestamp;

    public Percept3D(Node3D? target, Vector3 position, Vector3 velocity, Identity identity, float confidence,
        MemoryDecayStrategy decayStrategy)
    {
        this.Target = target;
        this.LastKnownPosition = position;
        this.LastKnownVelocity = velocity;
        this.Identity = identity;
        this.Confidence = Mathf.Clamp(confidence, 0.0f, 1.0f);
        this.DecayStrategy = decayStrategy;
        this.Timestamp = Time.GetTicksMsec();
    }
}
