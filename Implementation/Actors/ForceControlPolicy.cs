namespace Jmodot.Implementation.Actors;

using Godot;
using Jmodot.Core.Combat.EffectDefinitions;
using Jmodot.Core.Stats;

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
    /// Scales effective force magnitudes before they are compared against the thresholds above.
    /// 1.0 = full effect; lower values = entity resists capture forces; 0.0 = effective immunity.
    /// Assign a <see cref="ConstantFloatDefinition"/> for flat tuning, or an
    /// <see cref="AttributeFloatDefinition"/> to drive resistance from a per-entity stat. Null → 1.0.
    /// </summary>
    /// <remarks>
    /// The graded form is deliberate: a heavy or armoured entity resists capture proportionally with
    /// no per-entity code, where a binary immunity switch would need an enumerated exception list.
    /// </remarks>
    [ExportGroup("Stability")]
    [Export] public BaseFloatValueDefinition? StabilityMultiplier { get; set; }

    /// <summary>
    /// Resolves <see cref="StabilityMultiplier"/> against <paramref name="stats"/>, falling back to
    /// 1.0 (full effect) when unauthored. Every consumer resolves through here so the fallback has
    /// one home and the two capture conditions cannot drift apart on it.
    /// </summary>
    public float ResolveStabilityMultiplier(IStatProvider? stats) =>
        this.StabilityMultiplier?.ResolveFloatValue(stats) ?? 1.0f;
}
