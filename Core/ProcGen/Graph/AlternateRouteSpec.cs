namespace Jmodot.Core.ProcGen.Graph;

using System.Collections.Generic;
using System.Linq;
using Godot;
using Jmodot.Core.Shared;
using Jmodot.Implementation.Shared.GodotExceptions;

/// <summary>
///     The isolated spec for alternate routes (parallel paths off the spine): how many GUARANTEED
///     loops co-planned with the spine backbone (<see cref="GuaranteedCount" />), how many extra
///     OPPORTUNISTIC loops decorated afterward (<see cref="OpportunisticCount" />), how long each
///     (<see cref="Length" />), the minimum spine separation between a loop's divergence and rejoin
///     endpoints (<see cref="MinAnchorSeparation" />), where they attach
///     (<see cref="AttachmentWeights" />), plus structure-local placement rules. Draws from the
///     global config; never cross-references the spine or branch specs (two-tier isolation).
/// </summary>
[GlobalClass, Tool]
public sealed partial class AlternateRouteSpec : Resource
{
    /// <summary>Inclusive count of GUARANTEED loops — co-planned with the spine, closure guaranteed (anchors reserved during spine layout). Null leaves it to the generator default. Backbone feasibility: the consuming profile requires Spine.Length.Min ≥ GuaranteedCount.Min × MinAnchorSeparation + 3.</summary>
    [ExportGroup("Topology")]
    [Export] public IntRange? GuaranteedCount { get; private set; }

    /// <summary>Inclusive count of OPPORTUNISTIC loops — extra best-effort routes decorated after the backbone; soft-skipped if they cannot close. Null leaves it to the generator default.</summary>
    [Export] public IntRange? OpportunisticCount { get; private set; }

    /// <summary>Inclusive node-count range per alternate route. Null leaves it to the generator default.</summary>
    [Export] public IntRange? Length { get; private set; }

    /// <summary>Minimum spine separation (DistanceFromSource gap) between a guaranteed loop's divergence X and rejoin Y. <c>&gt;= 2</c> keeps the loop non-degenerate (avoids a 1-segment loop). Read by the generator's anchor-pair eligibility and by the consuming profile's backbone-feasibility check (Spine.Length.Min ≥ GuaranteedCount.Min × this + 3).</summary>
    [Export(PropertyHint.Range, "1,16,or_greater")] public int MinAnchorSeparation { get; private set; } = 2;

    /// <summary>SOFT biases scoring how attractive a node is as a route ENDPOINT (divergence X / rejoin Y; see <see cref="EndpointWeight" />). A distinct family from <see cref="Weights" /> (room-selection placement scoring). Empty slots dropped by <see cref="EffectiveAttachmentWeights" />.</summary>
    [ExportGroup("Placement Rules")]
    [Export] public Godot.Collections.Array<EndpointWeight?> AttachmentWeights { get; private set; } = new();

    /// <summary>Structure-local HARD filters for route placements (combined with global constraints). Empty slots dropped by <see cref="EffectiveConstraints" />.</summary>
    [Export] public Godot.Collections.Array<SlotConstraint?> Constraints { get; private set; } = new();

    /// <summary>Structure-local SOFT biases for route placements (combined with global weights). Empty slots dropped by <see cref="EffectiveWeights" />.</summary>
    [Export] public Godot.Collections.Array<SlotWeight?> Weights { get; private set; } = new();

    /// <summary>Null-filtered view of <see cref="AttachmentWeights" />, recomputed per read.</summary>
    public IReadOnlyList<EndpointWeight> EffectiveAttachmentWeights =>
        this.AttachmentWeights.Where(w => w != null).Cast<EndpointWeight>().ToList();

    /// <summary>Null-filtered view of <see cref="Constraints" />, recomputed per read.</summary>
    public IReadOnlyList<SlotConstraint> EffectiveConstraints =>
        this.Constraints.Where(c => c != null).Cast<SlotConstraint>().ToList();

    /// <summary>Null-filtered view of <see cref="Weights" />, recomputed per read.</summary>
    public IReadOnlyList<SlotWeight> EffectiveWeights =>
        this.Weights.Where(w => w != null).Cast<SlotWeight>().ToList();

    /// <summary>Per-knob fail-fast: validates <see cref="GuaranteedCount" />, <see cref="OpportunisticCount" />, <see cref="Length" /> (Min≤Max, Min≥0), <see cref="MinAnchorSeparation" /> (≥1), and rejects null rule-array entries.</summary>
    public void Validate()
    {
        this.GuaranteedCount?.Validate();
        this.OpportunisticCount?.Validate();
        this.Length?.Validate();
        if (this.GuaranteedCount != null && this.GuaranteedCount.Min < 0)
        {
            throw new ResourceConfigurationException(
                $"{nameof(AlternateRouteSpec)}.{nameof(this.GuaranteedCount)}.Min must be non-negative.", this);
        }
        if (this.OpportunisticCount != null && this.OpportunisticCount.Min < 0)
        {
            throw new ResourceConfigurationException(
                $"{nameof(AlternateRouteSpec)}.{nameof(this.OpportunisticCount)}.Min must be non-negative.", this);
        }
        if (this.Length != null && this.Length.Min < 0)
        {
            throw new ResourceConfigurationException(
                $"{nameof(AlternateRouteSpec)}.{nameof(this.Length)}.Min must be non-negative.", this);
        }
        if (this.MinAnchorSeparation < 1)
        {
            throw new ResourceConfigurationException(
                $"{nameof(AlternateRouteSpec)}.{nameof(this.MinAnchorSeparation)} must be >= 1.", this);
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
    internal void SetGuaranteedCount(IntRange? value) => this.GuaranteedCount = value;
    internal void SetOpportunisticCount(IntRange? value) => this.OpportunisticCount = value;
    internal void SetLength(IntRange? value) => this.Length = value;
    internal void SetMinAnchorSeparation(int value) => this.MinAnchorSeparation = value;
    internal void SetAttachmentWeights(Godot.Collections.Array<EndpointWeight?> value) => this.AttachmentWeights = value;
    internal void SetConstraints(Godot.Collections.Array<SlotConstraint?> value) => this.Constraints = value;
    internal void SetWeights(Godot.Collections.Array<SlotWeight?> value) => this.Weights = value;
#endif
    #endregion
}
