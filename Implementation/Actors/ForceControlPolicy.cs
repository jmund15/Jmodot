namespace Jmodot.Implementation.Actors;

using Godot;

/// <summary>
/// Per-entity policy bundling control-loss thresholds. Used by
/// <see cref="ForceControlLossDetector"/> to decide whether sustained source-filtered
/// force/offset magnitudes from <see cref="ExternalForceReceiver3D"/> constitute "capture."
///
/// Designer-tunable per-entity via .tres assets — wizard, enemies, bosses can each carry
/// their own policy with appropriate thresholds. Single concrete class today; subclassing
/// is the planned extension path for future strategies (windup-accumulating winds,
/// stat-modulated stability resistance). When a second strategy actually lands, this
/// becomes an abstract base + ThresholdForceControlPolicy subclass — until then,
/// simplicity wins.
/// </summary>
[GlobalClass]
public partial class ForceControlPolicy : Resource
{
    [ExportGroup("Force Axis")]

    /// <summary>
    /// When false, the force axis is ignored entirely — magnitudes from capture-tagged
    /// IForceProvider3D contributors do not contribute to control-loss evaluation.
    /// Used for entities immune to force-based capture (rooted bosses, heavy armor).
    /// </summary>
    [Export] public bool EnableForceAxis { get; set; } = true;

    /// <summary>Force magnitude that must be sustained for one evaluation tick to trigger capture.</summary>
    [Export(PropertyHint.Range, "0.0,50.0,0.1")] public float ForceLossThreshold { get; set; } = 5.0f;

    /// <summary>Force magnitude must drop below this for capture to release (hysteresis lower bound).</summary>
    [Export(PropertyHint.Range, "0.0,50.0,0.1")] public float ForceRegainThreshold { get; set; } = 1.0f;

    [ExportGroup("Offset Axis")]

    /// <summary>
    /// When false, the offset axis is ignored entirely. Used for entities immune to
    /// drag/carry effects from waves, currents, conveyors-tagged-as-capture, etc.
    /// </summary>
    [Export] public bool EnableOffsetAxis { get; set; } = true;

    /// <summary>Offset magnitude that must be sustained for one evaluation tick to trigger capture.</summary>
    [Export(PropertyHint.Range, "0.0,50.0,0.1")] public float OffsetLossThreshold { get; set; } = 3.0f;

    /// <summary>Offset magnitude must drop below this for capture to release (hysteresis lower bound).</summary>
    [Export(PropertyHint.Range, "0.0,50.0,0.1")] public float OffsetRegainThreshold { get; set; } = 0.5f;

    [ExportGroup("Future: Stability Integration")]

    /// <summary>
    /// Reserved seam for the stability stat. When IStatProvider infrastructure lands,
    /// this multiplier will be replaced by a stat-driven lookup that scales effective
    /// force/offset magnitudes before threshold comparison. Today: a static knob.
    /// 1.0 = full effect; lower values = entity resists capture forces; 0.0 = effective immunity.
    /// </summary>
    [Export(PropertyHint.Range, "0.0,2.0,0.05")] public float StabilityMultiplier { get; set; } = 1.0f;
}
