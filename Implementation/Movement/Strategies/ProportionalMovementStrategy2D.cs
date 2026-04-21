namespace Jmodot.Implementation.Movement.Strategies;

using Core.Shared.Attributes;
using Core.Stats;

/// <summary>
/// 2D movement strategy using proportional (damped-harmonic) friction combined with unbounded
/// acceleration. Unlike <see cref="LinearMovementStrategy2D"/> which clamps to a target speed via
/// MoveToward, this strategy lets acceleration and proportional friction reach natural equilibrium:
/// <c>v_eq = acceleration / friction</c>. Commonly chosen for top-down characters whose feel was
/// hand-tuned against the equation <c>v -= v*friction*delta; v += input*accel*delta</c>.
///
/// Semantics per frame:
///   newVelocity = currentVelocity - (currentVelocity * friction * FrictionMultiplier * delta)
///   if input present: newVelocity += desiredDirection * acceleration * delta
///
/// The <see cref="FrictionMultiplier"/> knob lets derivative states (e.g. a turn/braking state)
/// boost the damping rate without authoring separate friction attributes.
///
/// Note: does NOT consume a max-speed attribute — equilibrium speed is emergent from accel/friction.
/// If you need a hard speed cap, use <see cref="LinearMovementStrategy2D"/> instead.
/// </summary>
[GlobalClass, Tool]
public partial class ProportionalMovementStrategy2D : BaseMovementStrategy2D
{
    [ExportGroup("Stat Bindings")]
    [Export, RequiredExport] private Attribute _accelerationAttr = null!;
    [Export, RequiredExport] private Attribute _frictionAttr = null!;

    /// <summary>
    /// Per-instance multiplier on the friction stat. Use values &gt; 1.0 for turn/stopping states that
    /// need sharper decel; keep at 1.0 for normal movement. Multiplies the final damping force:
    /// <c>frictionApplied = friction * FrictionMultiplier</c>.
    /// Range hint prevents negative values (which would invert damping into velocity amplification)
    /// while keeping an <c>or_greater</c> escape for expert tuning beyond the typical [0, 10] window.
    /// </summary>
    [ExportGroup("Instance Tuning")]
    [Export(PropertyHint.Range, "0,10,0.01,or_greater")]
    public float FrictionMultiplier { get; set; } = 1.0f;

    public override Vector2 CalculateVelocity(Vector2 currentVelocity, Vector2 desiredDirection,
        Vector2 previousDirection, IStatProvider stats, float delta)
    {
        var friction = stats.GetStatValue<float>(_frictionAttr) * FrictionMultiplier;
        var accel = stats.GetStatValue<float>(_accelerationAttr);

        // Proportional damping — always applied. At high speed the damping force is large;
        // at low speed it is small. This produces the organic settle behavior characteristic
        // of hand-rolled top-down movement.
        var newVelocity = currentVelocity - (currentVelocity * friction * delta);

        // Unbounded acceleration — input adds a constant per-frame impulse. Equilibrium speed
        // emerges where friction*v_eq == accel, i.e. v_eq = accel / friction.
        if (!desiredDirection.IsZeroApprox())
        {
            newVelocity += desiredDirection * accel * delta;
        }

        return newVelocity;
    }

    #region Test Helpers
#if TOOLS
    internal void SetAccelerationAttrForTest(Attribute attr) => _accelerationAttr = attr;
    internal void SetFrictionAttrForTest(Attribute attr) => _frictionAttr = attr;
#endif
    #endregion
}
