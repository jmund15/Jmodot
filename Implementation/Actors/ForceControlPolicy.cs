namespace Jmodot.Implementation.Actors;

using Godot;

/// <summary>
/// Per-entity policy bundling control-loss thresholds. Consumed by HSM transition
/// conditions (<c>ControlLostCondition</c>/<c>ControlRegainedCondition</c>) to decide
/// whether sustained source-filtered force/offset magnitudes from
/// <see cref="ExternalForceReceiver3D"/> constitute "capture."
/// </summary>
/// <remarks>
/// Designer-tunable per-entity via .tres assets — wizard, enemies, bosses can each carry
/// their own policy with appropriate thresholds.
/// </remarks>
// Deliberately concrete, not abstract: one strategy exists today. When a second lands
// (windup-accumulating winds, stat-modulated stability resistance), this becomes an abstract
// base + ThresholdForceControlPolicy subclass. Until then, simplicity wins — don't pre-abstract.
[GlobalClass, Tool]
public partial class ForceControlPolicy : Resource
{
    /// <summary>
    /// In-memory fallback policy with the default thresholds. Used by conditions when
    /// no <see cref="ForceControlPolicy"/> Export is wired on the scene — keeps
    /// null-policy callers safe instead of forcing every actor to author a .tres.
    /// </summary>
    public static readonly ForceControlPolicy Default = new();

    /// <summary>
    /// When false, the force axis is ignored entirely — magnitudes from capture-tagged
    /// <see cref="Jmodot.Core.Environment.IForceProvider3D"/> contributors do not contribute to control-loss evaluation.
    /// Used for entities immune to force-based capture (rooted bosses, heavy armor).
    /// </summary>
    [ExportGroup("Force Axis")]
    [Export] public bool EnableForceAxis { get; set; } = true;

    /// <summary>Force magnitude that must be sustained for one evaluation tick to trigger capture.</summary>
    [Export(PropertyHint.Range, "0.0,50.0,0.1")] public float ForceLossThreshold { get; set; } = 5.0f;

    /// <summary>Force magnitude must drop below this for capture to release (hysteresis lower bound).</summary>
    [Export(PropertyHint.Range, "0.0,50.0,0.1")] public float ForceRegainThreshold { get; set; } = 1.0f;

    /// <summary>
    /// When false, the offset axis is ignored entirely. Used for entities immune to
    /// drag/carry effects from waves, currents, conveyors-tagged-as-capture, etc.
    /// </summary>
    [ExportGroup("Offset Axis")]
    [Export] public bool EnableOffsetAxis { get; set; } = true;

    /// <summary>Offset magnitude that must be sustained for one evaluation tick to trigger capture.</summary>
    [Export(PropertyHint.Range, "0.0,50.0,0.1")] public float OffsetLossThreshold { get; set; } = 3.0f;

    /// <summary>Offset magnitude must drop below this for capture to release (hysteresis lower bound).</summary>
    [Export(PropertyHint.Range, "0.0,50.0,0.1")] public float OffsetRegainThreshold { get; set; } = 0.5f;

    /// <summary>
    /// Scales effective force/offset magnitudes before they are compared against the
    /// thresholds above. 1.0 = full effect; lower values = entity resists capture forces;
    /// 0.0 = effective immunity.
    /// </summary>
    // Reserved seam: when IStatProvider infrastructure lands, replace this static knob with a
    // stat-driven lookup rather than adding a parallel stat-scaling surface beside it.
    [ExportGroup("Future: Stability Integration")]
    [Export(PropertyHint.Range, "0.0,2.0,0.05")] public float StabilityMultiplier { get; set; } = 1.0f;
}
