namespace Jmodot.Core.ProcGen.Graph;

using System.Collections.Generic;
using System.Linq;
using Godot;
using Jmodot.Core.Shared;
using Jmodot.Implementation.Shared.GodotExceptions;

/// <summary>
///     The isolated spec for alternate routes (parallel paths off the spine): how many
///     (<see cref="Count" />), how long each (<see cref="Length" />), where they attach
///     (<see cref="AttachmentWeights" />), plus structure-local placement rules. Draws from the
///     global config; never cross-references the spine or branch specs (two-tier isolation).
/// </summary>
[GlobalClass, Tool]
public sealed partial class AlternateRouteSpec : Resource
{
    /// <summary>Inclusive count of alternate routes to generate. Null leaves it to the generator default.</summary>
    [ExportGroup("Topology")]
    [Export] public IntRange? Count { get; private set; }

    /// <summary>Inclusive node-count range per alternate route. Null leaves it to the generator default.</summary>
    [Export] public IntRange? Length { get; private set; }

    /// <summary>SOFT biases scoring which spine node a route attaches to. Empty slots dropped by <see cref="EffectiveAttachmentWeights" />.</summary>
    [ExportGroup("Placement Rules")]
    [Export] public Godot.Collections.Array<SlotWeight?> AttachmentWeights { get; private set; } = new();

    /// <summary>Structure-local HARD filters for route placements (combined with global constraints). Empty slots dropped by <see cref="EffectiveConstraints" />.</summary>
    [Export] public Godot.Collections.Array<SlotConstraint?> Constraints { get; private set; } = new();

    /// <summary>Structure-local SOFT biases for route placements (combined with global weights). Empty slots dropped by <see cref="EffectiveWeights" />.</summary>
    [Export] public Godot.Collections.Array<SlotWeight?> Weights { get; private set; } = new();

    /// <summary>Null-filtered view of <see cref="AttachmentWeights" />, recomputed per read.</summary>
    public IReadOnlyList<SlotWeight> EffectiveAttachmentWeights =>
        this.AttachmentWeights.Where(w => w != null).Cast<SlotWeight>().ToList();

    /// <summary>Null-filtered view of <see cref="Constraints" />, recomputed per read.</summary>
    public IReadOnlyList<SlotConstraint> EffectiveConstraints =>
        this.Constraints.Where(c => c != null).Cast<SlotConstraint>().ToList();

    /// <summary>Null-filtered view of <see cref="Weights" />, recomputed per read.</summary>
    public IReadOnlyList<SlotWeight> EffectiveWeights =>
        this.Weights.Where(w => w != null).Cast<SlotWeight>().ToList();

    /// <summary>Per-knob fail-fast: validates <see cref="Count" /> and <see cref="Length" /> (Min≤Max) and rejects null rule-array entries.</summary>
    public void Validate()
    {
        this.Count?.Validate();
        this.Length?.Validate();
        if (this.Count != null && this.Count.Min < 0)
        {
            throw new ResourceConfigurationException(
                $"{nameof(AlternateRouteSpec)}.{nameof(this.Count)}.Min must be non-negative.", this);
        }
        if (this.Length != null && this.Length.Min < 0)
        {
            throw new ResourceConfigurationException(
                $"{nameof(AlternateRouteSpec)}.{nameof(this.Length)}.Min must be non-negative.", this);
        }
        if (this.AttachmentWeights.Any(w => w is null))
        {
            throw new ResourceConfigurationException(
                $"{nameof(AlternateRouteSpec)}.{nameof(this.AttachmentWeights)} contains a null entry (empty Inspector slot).", this);
        }
        if (this.Constraints.Any(c => c is null))
        {
            throw new ResourceConfigurationException(
                $"{nameof(AlternateRouteSpec)}.{nameof(this.Constraints)} contains a null entry (empty Inspector slot).", this);
        }
        if (this.Weights.Any(w => w is null))
        {
            throw new ResourceConfigurationException(
                $"{nameof(AlternateRouteSpec)}.{nameof(this.Weights)} contains a null entry (empty Inspector slot).", this);
        }
    }

    #region Test Helpers
#if TOOLS
    internal void SetCount(IntRange? value) => this.Count = value;
    internal void SetLength(IntRange? value) => this.Length = value;
    internal void SetAttachmentWeights(Godot.Collections.Array<SlotWeight?> value) => this.AttachmentWeights = value;
    internal void SetConstraints(Godot.Collections.Array<SlotConstraint?> value) => this.Constraints = value;
    internal void SetWeights(Godot.Collections.Array<SlotWeight?> value) => this.Weights = value;
#endif
    #endregion
}
