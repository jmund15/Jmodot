namespace Jmodot.Implementation.Actors;

using System;
using Core.Actors;
using Core.Movement;
using Core.Pooling;
using Shared;

/// <summary>
/// Detects when sustained environmental force or velocity-offset providers (registered
/// on <see cref="ExternalForceReceiver3D"/>) overwhelm the entity's normal control.
/// Source-based by design: reads only provider-aggregate magnitudes from the receiver,
/// so transient combat impulses (which never register as providers) are invisible here
/// and route through the tag-based hit-reaction states instead. Exposes typed state +
/// events so consumers (HSM states, BT actions) can subscribe without stringly-typed BB
/// flags.
/// </summary>
/// <remarks>
/// Architecture: each axis (force vs. velocity-offset) is evaluated with its own
/// hysteresis band against thresholds from a per-entity <see cref="ForceControlPolicy"/>
/// resource, then OR-merged. See <see cref="SourceBasedControlLossEvaluator"/>. The policy
/// also carries axis-disabling toggles and a StabilityMultiplier seam (future stat hook).
/// If no Policy is assigned, a static default policy with documented defaults is used.
/// </remarks>
[GlobalClass]
public partial class ForceControlLossDetector : Node, IPoolResetable
{
    /// <summary>
    /// Per-entity tuning of capture thresholds + axis enables + stability multiplier.
    /// When null, the static default policy is used (5.0/1.0 force, 3.0/0.5 offset, both
    /// axes enabled, stability=1.0). Authoring guidance: create a .tres file per entity
    /// archetype and reuse across scenes.
    /// </summary>
    [Export] public ForceControlPolicy? Policy { get; set; }

    private static readonly ForceControlPolicy _defaultPolicy = new();

    [Export(PropertyHint.Range, "0.016,0.5,0.001")]
    public float EvaluationInterval { get; set; } = 0.1f;

    public bool IsControlLost { get; private set; }
    public ForceContext? CurrentContext { get; private set; }

    public event Action<ForceContext> ControlLost = delegate { };
    public event Action ControlRegained = delegate { };

    private ICharacterController3D? _controller;
    private ExternalForceReceiver3D? _forceReceiver;
    private Node3D? _owner;

    private float _timeSinceLastEval;

    public void Initialize(
        ICharacterController3D controller,
        ExternalForceReceiver3D forceReceiver,
        Node3D owner)
    {
        _controller = controller;
        _forceReceiver = forceReceiver;
        _owner = owner;
        IsControlLost = false;
        CurrentContext = null;
        _timeSinceLastEval = 0f;
    }

    public void OnPoolReset()
    {
        IsControlLost = false;
        CurrentContext = null;
        _timeSinceLastEval = 0f;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_forceReceiver == null || _owner == null)
        {
            return;
        }

        _timeSinceLastEval += (float)delta;
        if (_timeSinceLastEval < EvaluationInterval)
        {
            return;
        }

        _timeSinceLastEval = 0f;

        var policy = Policy ?? _defaultPolicy;

        // Capture-filtered: gravity and other non-capture IForceProvider3D contributions
        // are excluded by the receiver. Stability multiplier scales magnitudes uniformly
        // before the threshold check; axis-disable forces the magnitude to 0 so the
        // evaluator's hysteresis can never trip on that axis.
        var rawForceMag = _forceReceiver.GetCaptureForce(_owner).Length();
        var rawOffsetMag = _forceReceiver.GetCaptureVelocityOffset(_owner).Length();

        var forceMagnitude = policy.EnableForceAxis ? rawForceMag * policy.StabilityMultiplier : 0f;
        var offsetMagnitude = policy.EnableOffsetAxis ? rawOffsetMag * policy.StabilityMultiplier : 0f;

        var shouldBeLost = SourceBasedControlLossEvaluator.ShouldLoseControl(
            forceMagnitude,
            offsetMagnitude,
            IsControlLost,
            policy.ForceLossThreshold,
            policy.ForceRegainThreshold,
            policy.OffsetLossThreshold,
            policy.OffsetRegainThreshold);

        if (shouldBeLost == IsControlLost)
        {
            return;
        }

        IsControlLost = shouldBeLost;

        var policyLabel = Policy?.ResourcePath is { Length: > 0 } path ? path : "<default>";

        if (IsControlLost)
        {
            var (dominantSource, dominantForce) = _forceReceiver.GetDominantForceSource(_owner);
            CurrentContext = new ForceContext
            {
                DominantForceDirection = dominantForce.Normalized(),
                ForceMagnitude = dominantForce.Length(),
                DominantSource = dominantSource,
            };

            var providers = _forceReceiver.DescribeActiveCaptureProviders(_owner);
            JmoLogger.Info(this,
                $"Control LOST — captureForce={forceMagnitude:F2} (raw {rawForceMag:F2}, threshold {policy.ForceLossThreshold:F2}, axis {(policy.EnableForceAxis ? "on" : "off")}), "
                + $"captureOffset={offsetMagnitude:F2} (raw {rawOffsetMag:F2}, threshold {policy.OffsetLossThreshold:F2}, axis {(policy.EnableOffsetAxis ? "on" : "off")}), "
                + $"stabilityMul={policy.StabilityMultiplier:F2}, policy={policyLabel}, "
                + $"dominant={dominantSource?.Name ?? "unknown"}, providers=[{providers}]");

            ControlLost.Invoke(CurrentContext);
        }
        else
        {
            CurrentContext = null;

            JmoLogger.Info(this,
                $"Control REGAINED — captureForce={forceMagnitude:F2} (regain {policy.ForceRegainThreshold:F2}), "
                + $"captureOffset={offsetMagnitude:F2} (regain {policy.OffsetRegainThreshold:F2}), "
                + $"policy={policyLabel}");

            ControlRegained.Invoke();
        }
    }

    #region Test Helpers
#if TOOLS
    /// <summary>
    /// Test-only setter that drives <see cref="IsControlLost"/> + <see cref="CurrentContext"/>
    /// directly and raises the appropriate edge event. Bypasses the velocity-based hysteresis
    /// evaluator so consumer-side tests can exercise rising/falling-edge paths without setting
    /// up a full physics scene.
    /// </summary>
    /// <remarks>
    /// Used by ControlLossConditionTests + WizardCapturedByWaveTests behavioral suite. Wrapped
    /// in <c>#if TOOLS</c> per the project's test-helper convention so it does NOT ship in
    /// release builds.
    /// </remarks>
    internal void SetIsControlLostForTesting(bool isLost, ForceContext? context = null)
    {
        if (isLost == IsControlLost)
        {
            return;
        }

        IsControlLost = isLost;
        if (isLost)
        {
            CurrentContext = context ?? new ForceContext();
            ControlLost.Invoke(CurrentContext);
        }
        else
        {
            CurrentContext = null;
            ControlRegained.Invoke();
        }
    }
#endif
    #endregion
}
