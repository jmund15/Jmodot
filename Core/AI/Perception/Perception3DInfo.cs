namespace Jmodot.Core.AI.Perception;

using Identification;
using Implementation.AI.Perception.Strategies;

/// <summary>
///     A stateful class representing an AI's "living memory" of a single target. It manages
///     a two-state lifecycle: "actively sensed" (confidence = sensor value) and "remembered"
///     (confidence = decay strategy applied to last sensed value).
/// </summary>
public class Perception3DInfo
{
    private float _baseConfidence;
    private MemoryDecayStrategy _decayStrategy = null!;
    private bool _sensingActive = true;
    private ulong _exitTime;

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
    ///     Calculates and returns the current confidence for this memory.
    ///     While actively sensed, returns the raw sensor confidence.
    ///     After exit, delegates to the decay strategy.
    /// </summary>
    public float CurrentConfidence
    {
        get
        {
            if (_sensingActive) { return _baseConfidence; }
            var elapsed = (Time.GetTicksMsec() - _exitTime) / 1000f;
            return _decayStrategy?.CalculateConfidence(_baseConfidence, elapsed) ?? 0f;
        }
    }

    /// <summary>Returns true if the memory is still considered active (confidence is above zero).</summary>
    public bool IsActive => this.CurrentConfidence > 0.001f;

    /// <summary>
    ///     Updates the memory record with information from a new, incoming percept.
    ///     Exit events (confidence ≤ epsilon) transition to "remembered" state.
    ///     Active detections refresh the sensing state.
    /// </summary>
    public void Update(Percept3D latestPercept3D)
    {
        if (latestPercept3D.Confidence <= 0.001f)
        {
            _sensingActive = false;
            _exitTime = latestPercept3D.Timestamp;
            return;
        }

        _sensingActive = true;
        this.LastKnownPosition = latestPercept3D.LastKnownPosition;
        this.LastKnownVelocity = latestPercept3D.LastKnownVelocity;
        this.Identity = latestPercept3D.Identity;
        this._baseConfidence = latestPercept3D.Confidence;
        this._decayStrategy = latestPercept3D.DecayStrategy;
        this.LastUpdateTime = latestPercept3D.Timestamp;
    }
}
