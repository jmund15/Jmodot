namespace Jmodot.Core.Environment.FlowField;

/// <summary>
///     The framework-owned boundary for flow-field configuration: Jmodot declares the contract, the
///     consuming game implements it on whatever Resource it likes. Carries the real runtime data a
///     flow-field force area consumes — never an empty pattern-matching marker.
/// </summary>
/// <remarks>
///     Reference identity of the implementing instance doubles as the entity-merge gate: two flow-field
///     entities may only merge when they carry the SAME profile instance, so cross-profile fields always
///     coexist instead of corrupting each other's identity and visuals. See
///     <see cref="FlowFieldMath.AreEntitiesMergeable" />.
/// </remarks>
public interface IFlowFieldProfile
{
    /// <summary>Baked from the authored falloff Curve: normalized distance-from-spine to influence weight.</summary>
    float[] RadialFalloff01 { get; }

    /// <summary>Baked from the authored speed Curve: normalized energy to applied velocity magnitude.</summary>
    float[] EnergyToSpeed01 { get; }

    /// <summary>Which force seam this field drives.</summary>
    FlowForceFlavor Flavor { get; }

    /// <summary>
    ///     Whether this field counts toward control-loss detection. Feeds BOTH
    ///     <see cref="IForceProvider3D.IsCaptureForce" /> and
    ///     <see cref="IVelocityOffsetProvider3D.IsCaptureOffset" /> — they are separate interface members
    ///     with independent <c>=&gt; false</c> defaults, so setting only one silently leaves a
    ///     capture-flavored field feeding neither aggregate.
    /// </summary>
    bool IsCapture { get; }
}
