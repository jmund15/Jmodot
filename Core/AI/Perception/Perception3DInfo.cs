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
    public float CurrentConfidence => GetConfidenceAt(Time.GetTicksMsec());

    /// <summary>Returns true if the memory is still considered active (confidence is above zero).</summary>
    public bool IsActive => this.CurrentConfidence > 0.001f;

    /// <summary>
    ///     Calculates confidence at a specific point in time.
    ///     While actively sensed, returns the raw sensor confidence regardless of time.
    ///     After exit, applies the decay strategy using elapsed time since exit.
    /// </summary>
    public float GetConfidenceAt(ulong currentTimeMs)
    {
        if (_sensingActive) { return _baseConfidence; }
        var elapsed = (currentTimeMs - _exitTime) / 1000f;
        return _decayStrategy?.CalculateConfidence(_baseConfidence, elapsed) ?? 0f;
    }

    /// <summary>
    ///     Projects the target's position forward using its last known velocity,
    ///     scaled by confidence and projectionInfluence to prevent wild extrapolation.
    ///     While actively sensed (timeSinceUpdate ≈ 0), returns approximately LastKnownPosition.
    ///     For decaying memories, approximates where the target is NOW rather than where it WAS.
    /// </summary>
    /// <param name="projectionInfluence">0.0 = no projection (raw position), 1.0 = full projection.
    /// Gives consumers (considerations, escape checks) control over how much velocity prediction to use.</param>
    public Vector3 GetProjectedPosition(float projectionInfluence = 1f)
        => GetProjectedPosition(Time.GetTicksMsec(), projectionInfluence);

    /// <summary>
    ///     Test-friendly overload that accepts an explicit timestamp for deterministic testing.
    /// </summary>
    public Vector3 GetProjectedPosition(ulong currentTimeMs, float projectionInfluence = 1f)
    {
        if (projectionInfluence < 0.0001f || LastKnownVelocity.LengthSquared() < 0.0001f)
        {
            return LastKnownPosition;
        }

        float timeSinceUpdate = (currentTimeMs - LastUpdateTime) / 1000f;
        if (timeSinceUpdate <= 0f)
        {
            return LastKnownPosition;
        }

        float confidence = GetConfidenceAt(currentTimeMs);
        return LastKnownPosition + LastKnownVelocity * timeSinceUpdate * confidence * projectionInfluence;
    }

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
