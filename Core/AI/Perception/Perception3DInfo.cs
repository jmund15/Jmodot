namespace Jmodot.Core.AI.Perception;

using Identification;
using Implementation.AI.Perception.Strategies;

/// <summary>
///     A stateful class representing an AI's "living memory" of a single target. It is created
///     and managed by the AIPerceptionManager and applies a decay strategy to its confidence,
///     providing a dynamic and realistic memory model.
/// </summary>
public class Perception3DInfo
{
    private float _baseConfidence;
    private MemoryDecayStrategy _decayStrategy = null!;

    public Perception3DInfo(Percept3D initialPercept3D)
    {
        this.Target = initialPercept3D.Target;
        this.Update(initialPercept3D);
    }

    /// <summary>The target Node this memory pertains to.</summary>
    public Node3D Target { get; }

    public Vector3 LastKnownPosition { get; private set; }
    public Vector3 LastKnownVelocity { get; private set; }
    public Identity Identity { get; private set; }
    public ulong LastUpdateTime { get; private set; }

    /// <summary>
    ///     Calculates and returns the current, time-decayed confidence for this memory. This is an
    ///     efficient, on-demand property that performs the calculation only when asked.
    /// </summary>
    public float CurrentConfidence =>
        this._decayStrategy?.CalculateConfidence(this._baseConfidence,
            (Time.GetTicksMsec() - this.LastUpdateTime) / 1000.0f) ?? this._baseConfidence;

    /// <summary>Returns true if the memory is still considered active (confidence is above zero).</summary>
    public bool IsActive => this.CurrentConfidence > 0.001f; // Use a small epsilon to avoid floating point issues.

    /// <summary>Updates the memory record with information from a new, incoming percept.</summary>
    public void Update(Percept3D latestPercept3D)
    {
        this.LastKnownPosition = latestPercept3D.LastKnownPosition;
        this.LastKnownVelocity = latestPercept3D.LastKnownVelocity;
        this.Identity = latestPercept3D.Identity;
        this._baseConfidence = latestPercept3D.Confidence;
        this._decayStrategy = latestPercept3D.DecayStrategy;
        this.LastUpdateTime = latestPercept3D.Timestamp;
    }
}
