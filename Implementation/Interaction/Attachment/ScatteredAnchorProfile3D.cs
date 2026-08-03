namespace Jmodot.Implementation.Interaction.Attachment;

using System;
using System.Collections.Generic;
using Godot;
using Jmodot.Implementation.Visual;

/// <summary>
/// Riders scattered across the silhouette by rejection sampling: each candidate is drawn uniformly
/// inside the measured bounds and kept only if it clears every anchor already taken. The right rule
/// whenever riders should look swarmed rather than arranged.
/// </summary>
[GlobalClass, Tool]
public partial class ScatteredAnchorProfile3D : AttachmentAnchorProfile3D
{
    /// <summary>
    /// Fraction of the silhouette's dominant extent one unit of footprint claims. Bounds-relative so
    /// a large host spreads its riders out rather than clustering them at a fixed metre spacing.
    /// </summary>
    [Export] public float SeparationRatioPerFootprintUnit { get; private set; } = 0.2f;

    /// <summary>
    /// Rejected candidates before placement gives up. Bounded because a saturated silhouette would
    /// otherwise spin forever, and "host is full" is a legitimate answer.
    /// </summary>
    [Export] public int MaxPlacementAttempts { get; private set; } = 12;

    /// <summary>
    /// Half-range, in metres, riders may sit off this host's sprite plane. 0 keeps placement exactly
    /// planar, which is correct for every silhouette whose art has no depth; raise it to give
    /// overlapping riders draw-order separation.
    /// </summary>
    [Export] public float DepthRange { get; private set; } = 0f;

    /// <inheritdoc />
    public override Vector3? Place(
        VisualBounds3D bounds,
        IReadOnlyList<Vector3> occupied,
        float footprint,
        Func<float> nextUnitFloat)
    {
        return AttachmentAnchorPlacer.Place(
            bounds,
            occupied,
            footprint,
            nextUnitFloat,
            this.SeparationRatioPerFootprintUnit,
            this.MaxPlacementAttempts,
            this.DepthRange);
    }

    #region Test Helpers
#if TOOLS
    internal void SetSeparationRatioPerFootprintUnit(float ratio)
        => this.SeparationRatioPerFootprintUnit = ratio;

    internal void SetMaxPlacementAttempts(int attempts) => this.MaxPlacementAttempts = attempts;

    internal void SetDepthRange(float depthRange) => this.DepthRange = depthRange;
#endif
    #endregion
}
