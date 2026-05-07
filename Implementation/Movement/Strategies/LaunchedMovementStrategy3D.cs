namespace Jmodot.Implementation.Movement.Strategies;

using Godot;
using Jmodot.Core.Combat.EffectDefinitions;
using Jmodot.Core.Shared.Attributes;
using Jmodot.Core.Stats;

/// <summary>
/// Movement strategy for entities that have just received a launch-magnitude impulse and are
/// coasting on the impulse-residue. Distinct from <see cref="StillMovementStrategy3D"/>
/// (which clobbers velocity to zero) — Launched preserves the entity's current velocity and
/// applies linear drag-decay over time, producing the "thrown puppet" feel rather than the
/// "frozen statue" feel.
///
/// CONTRACT: Ignores <c>desiredDirection</c> and <c>previousDirection</c> entirely. The
/// launched entity is not steering itself; the impulse is the only authority on motion.
///
/// USE: Push to <see cref="IMovementProcessor3D.SetStrategyOverride"/> when entering an HSM
/// state that represents control-loss after a force impulse (e.g., LaunchedState).
/// Pop on state exit so the agent's default locomotion strategy resumes integrating
/// player/AI input into velocity.
/// </summary>
[GlobalClass, Tool]
public partial class LaunchedMovementStrategy3D : BaseMovementStrategy3D
{
    /// <summary>
    /// Linear drag coefficient (units: 1/s). Velocity decays exponentially by
    /// <c>velocity *= max(0, 1 - LinearDrag * delta)</c> per frame. Designer-tunable for
    /// "feel" — higher = faster decay (snappier recovery), lower = longer slide.
    /// </summary>
    [ExportGroup("Drag Decay")]
    [Export, RequiredExport] public BaseFloatValueDefinition LinearDrag { get; private set; } = null!;

    /// <summary>
    /// If true, only the horizontal (X/Z) components of velocity decay; vertical (Y)
    /// passes through unchanged so gravity / jump-arc / wave-lift integrators in the
    /// movement pipeline can keep operating. If false, all three axes decay uniformly
    /// (use for fully-airborne ragdoll-style coasting).
    /// </summary>
    [Export] public bool RespectGravity { get; private set; } = true;

    public override Vector3 CalculateVelocity(
        Vector3 currentVelocity,
        Vector3 desiredDirection,
        Vector3 previousDirection,
        IStatProvider stats,
        float delta)
    {
        var drag = LinearDrag.ResolveFloatValue(stats);
        // Clamp the per-frame factor to [0, 1] so very high drag * very high delta cannot
        // flip the sign of the velocity (which would inject motion in the opposite direction
        // — a known "negative-friction" bug in naive linear-drag integrators).
        var factor = Mathf.Max(0f, 1f - drag * delta);

        if (RespectGravity)
        {
            return new Vector3(currentVelocity.X * factor, currentVelocity.Y, currentVelocity.Z * factor);
        }

        return currentVelocity * factor;
    }

    #region Test Helpers
#if TOOLS
    internal void SetLinearDrag(BaseFloatValueDefinition value) => LinearDrag = value;
    internal void SetRespectGravity(bool value) => RespectGravity = value;
#endif
    #endregion
}
