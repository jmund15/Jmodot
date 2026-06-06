namespace Jmodot.Core.ProcGen.Graph;

using System.Collections.Generic;
using System.Linq;
using Godot;
using Jmodot.Core.Shared;
using Jmodot.Implementation.Shared.GodotExceptions;

/// <summary>
///     The isolated spec for the main spine path: its node-count range plus structure-local
///     placement rules. Draws from the global config for shared context — never cross-references the
///     alternate-route or branch specs (the two-tier isolation invariant). Effective spine rules =
///     global ∪ these locals.
/// </summary>
[GlobalClass, Tool]
public sealed partial class SpineSpec : Resource
{
    /// <summary>Inclusive node-count range for the spine. Null leaves the bound to the generator default.</summary>
    [ExportGroup("Topology")]
    [Export] public IntRange? Length { get; private set; }

    /// <summary>Structure-local HARD filters for spine placements (combined with global constraints). Empty Inspector slots (null) are dropped by <see cref="EffectiveConstraints" />.</summary>
    [ExportGroup("Placement Rules")]
    [Export] public Godot.Collections.Array<SlotConstraint?> Constraints { get; private set; } = new();

    /// <summary>Structure-local SOFT biases for spine placements (combined with global weights). Empty slots dropped by <see cref="EffectiveWeights" />.</summary>
    [Export] public Godot.Collections.Array<SlotWeight?> Weights { get; private set; } = new();

    /// <summary>Null-filtered view of <see cref="Constraints" />, recomputed per read (no cached backing field — shared config holds zero mutable runtime state).</summary>
    public IReadOnlyList<SlotConstraint> EffectiveConstraints =>
        this.Constraints.Where(c => c != null).Cast<SlotConstraint>().ToList();

    /// <summary>Null-filtered view of <see cref="Weights" />, recomputed per read.</summary>
    public IReadOnlyList<SlotWeight> EffectiveWeights =>
        this.Weights.Where(w => w != null).Cast<SlotWeight>().ToList();

    /// <summary>Per-knob fail-fast: validates <see cref="Length" /> (Min≤Max via <c>IntRange.Validate</c>) and rejects null rule-array entries.</summary>
    public void Validate()
    {
        this.Length?.Validate();
        if (this.Length != null && this.Length.Min < 0)
        {
            throw new ResourceConfigurationException(
                $"{nameof(SpineSpec)}.{nameof(this.Length)}.Min must be non-negative (a node count cannot be negative).", this);
        }
        if (this.Constraints.Any(c => c is null))
        {
            throw new ResourceConfigurationException(
                $"{nameof(SpineSpec)}.{nameof(this.Constraints)} contains a null entry (empty Inspector slot).", this);
        }
        if (this.Weights.Any(w => w is null))
        {
            throw new ResourceConfigurationException(
                $"{nameof(SpineSpec)}.{nameof(this.Weights)} contains a null entry (empty Inspector slot).", this);
        }
    }

    #region Test Helpers
#if TOOLS
    internal void SetLength(IntRange? value) => this.Length = value;
    internal void SetConstraints(Godot.Collections.Array<SlotConstraint?> value) => this.Constraints = value;
    internal void SetWeights(Godot.Collections.Array<SlotWeight?> value) => this.Weights = value;
#endif
    #endregion
}
