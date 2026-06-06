namespace Jmodot.Core.ProcGen.Graph;

using System.Collections.Generic;
using System.Linq;
using Godot;
using Jmodot.Core.Shared;
using Jmodot.Implementation.Shared.GodotExceptions;

/// <summary>
///     The isolated spec for branches (dead-end offshoots): how many (<see cref="Density" />), how
///     deep each reaches (<see cref="Depth" />), and the branching factor (<see cref="FanOut" />),
///     plus structure-local placement rules. Draws from the global config; never cross-references
///     the spine or alternate-route specs (two-tier isolation). Depth bounds topology only —
///     recursive branch expansion is a generation-time (P3a.6) concern.
/// </summary>
[GlobalClass, Tool]
public sealed partial class BranchSpec : Resource
{
    /// <summary>Inclusive count of branches to grow off the graph. Null leaves it to the generator default.</summary>
    [ExportGroup("Topology")]
    [Export] public IntRange? Density { get; private set; }

    /// <summary>Inclusive per-branch depth (node reach from its attachment point). Null leaves it to the generator default.</summary>
    [Export] public IntRange? Depth { get; private set; }

    /// <summary>Inclusive branching factor (children per branch node). Null leaves it to the generator default.</summary>
    [Export] public IntRange? FanOut { get; private set; }

    /// <summary>Structure-local HARD filters for branch placements (combined with global constraints). Empty slots dropped by <see cref="EffectiveConstraints" />.</summary>
    [ExportGroup("Placement Rules")]
    [Export] public Godot.Collections.Array<SlotConstraint?> Constraints { get; private set; } = new();

    /// <summary>Structure-local SOFT biases for branch placements (combined with global weights). Empty slots dropped by <see cref="EffectiveWeights" />.</summary>
    [Export] public Godot.Collections.Array<SlotWeight?> Weights { get; private set; } = new();

    /// <summary>Null-filtered view of <see cref="Constraints" />, recomputed per read.</summary>
    public IReadOnlyList<SlotConstraint> EffectiveConstraints =>
        this.Constraints.Where(c => c != null).Cast<SlotConstraint>().ToList();

    /// <summary>Null-filtered view of <see cref="Weights" />, recomputed per read.</summary>
    public IReadOnlyList<SlotWeight> EffectiveWeights =>
        this.Weights.Where(w => w != null).Cast<SlotWeight>().ToList();

    /// <summary>Per-knob fail-fast: validates <see cref="Density" />, <see cref="Depth" />, <see cref="FanOut" /> (Min≤Max) and rejects null rule-array entries.</summary>
    public void Validate()
    {
        this.Density?.Validate();
        this.Depth?.Validate();
        this.FanOut?.Validate();
        if (this.Density != null && this.Density.Min < 0)
        {
            throw new ResourceConfigurationException(
                $"{nameof(BranchSpec)}.{nameof(this.Density)}.Min must be non-negative.", this);
        }
        if (this.Depth != null && this.Depth.Min < 0)
        {
            throw new ResourceConfigurationException(
                $"{nameof(BranchSpec)}.{nameof(this.Depth)}.Min must be non-negative.", this);
        }
        if (this.FanOut != null && this.FanOut.Min < 0)
        {
            throw new ResourceConfigurationException(
                $"{nameof(BranchSpec)}.{nameof(this.FanOut)}.Min must be non-negative.", this);
        }
        if (this.Constraints.Any(c => c is null))
        {
            throw new ResourceConfigurationException(
                $"{nameof(BranchSpec)}.{nameof(this.Constraints)} contains a null entry (empty Inspector slot).", this);
        }
        if (this.Weights.Any(w => w is null))
        {
            throw new ResourceConfigurationException(
                $"{nameof(BranchSpec)}.{nameof(this.Weights)} contains a null entry (empty Inspector slot).", this);
        }
    }

    #region Test Helpers
#if TOOLS
    internal void SetDensity(IntRange? value) => this.Density = value;
    internal void SetDepth(IntRange? value) => this.Depth = value;
    internal void SetFanOut(IntRange? value) => this.FanOut = value;
    internal void SetConstraints(Godot.Collections.Array<SlotConstraint?> value) => this.Constraints = value;
    internal void SetWeights(Godot.Collections.Array<SlotWeight?> value) => this.Weights = value;
#endif
    #endregion
}
