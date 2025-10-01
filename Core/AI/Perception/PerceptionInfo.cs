namespace Jmodot.Core.AI.Perception;

using Identification;
using Implementation.AI.Perception.Strategies;

/// <summary>
///     A stateful class representing an AI's "living memory" of a single target. It is created
///     and managed by the AIPerceptionManager and applies a decay strategy to its confidence,
///     providing a dynamic and realistic memory model.
/// </summary>
public class PerceptionInfo
{
    private float _baseConfidence;
    private MemoryDecayStrategy _decayStrategy = null!;

    public PerceptionInfo(Percept initialPercept)
    {
        this.Target = initialPercept.Target;
        this.Update(initialPercept);
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
    public void Update(Percept latestPercept)
    {
        this.LastKnownPosition = latestPercept.LastKnownPosition;
        this.LastKnownVelocity = latestPercept.LastKnownVelocity;
        this.Identity = latestPercept.Identity;
        this._baseConfidence = latestPercept.Confidence;
        this._decayStrategy = latestPercept.DecayStrategy;
        this.LastUpdateTime = latestPercept.Timestamp;
    }
}
