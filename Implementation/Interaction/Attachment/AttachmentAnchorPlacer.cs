namespace Jmodot.Implementation.Interaction.Attachment;

using System;
using System.Collections.Generic;
using Godot;
using Jmodot.Implementation.Shared;
using Jmodot.Implementation.Visual;

/// <summary>
/// Chooses where a rider sits on a host: a rejection-sampled, entity-local anchor inside the host's
/// measured silhouette that clears every anchor already taken. Pure — no node access and no
/// <c>JmoRng</c> field; randomness arrives as an injected roll delegate so the same roll sequence
/// always produces the same anchor.
/// </summary>
public static class AttachmentAnchorPlacer
{
    /// <summary>
    /// Rejected candidates before the placer gives up. Bounded because a saturated silhouette
    /// would otherwise spin forever, and "host is full" is a legitimate answer.
    /// </summary>
    private const int MaxPlacementAttempts = 12;

    /// <summary>
    /// Fraction of the silhouette's dominant extent one unit of footprint claims. Bounds-relative
    /// so a large host spreads its riders out rather than clustering them at a fixed metre spacing.
    /// </summary>
    private const float SeparationRatioPerFootprintUnit = 0.2f;

    /// <summary>
    /// Centre-to-centre distance a rider of <paramref name="footprint"/> needs from every anchor
    /// already taken on <paramref name="bounds"/>. Scales linearly with footprint, so a rider that
    /// occupies twice the capacity also claims twice the room.
    /// </summary>
    public static float MinSeparationFor(VisualBounds3D bounds, float footprint)
        => bounds.Largest * SeparationRatioPerFootprintUnit * Mathf.Max(footprint, 0f);

    /// <summary>
    /// An entity-local anchor for a rider of <paramref name="footprint"/>, or null when the host's
    /// art could not be measured or no attempt cleared <paramref name="occupied"/>. The anchor is
    /// PLANAR — offsets are measured from <see cref="VisualBounds3D.Center"/> and Z is always zero,
    /// because every shipped sprite silhouette has zero depth.
    /// </summary>
    /// <param name="nextUnitFloat">Roll source yielding values in [0, 1). Two rolls are consumed per attempt (X then Y).</param>
    public static Vector3? Place(
        VisualBounds3D bounds,
        IReadOnlyList<Vector3> occupied,
        float footprint,
        Func<float> nextUnitFloat)
    {
        if (!bounds.IsMeasured) { return null; }

        if (nextUnitFloat == null)
        {
            JmoLogger.Error(typeof(AttachmentAnchorPlacer),
                "Anchor placement requires an injected roll source; none was supplied.");
            return null;
        }

        var separation = MinSeparationFor(bounds, footprint);
        var separationSquared = separation * separation;

        for (var attempt = 0; attempt < MaxPlacementAttempts; attempt++)
        {
            var candidate = Sample(bounds, nextUnitFloat);
            if (IsClear(candidate, occupied, separationSquared)) { return candidate; }
        }

        return null;
    }

    private static Vector3 Sample(VisualBounds3D bounds, Func<float> nextUnitFloat)
    {
        var x = bounds.Center.X + ((nextUnitFloat() - 0.5f) * bounds.Width);
        var y = bounds.Center.Y + ((nextUnitFloat() - 0.5f) * bounds.Height);
        return new Vector3(x, y, 0f);
    }

    private static bool IsClear(
        Vector3 candidate, IReadOnlyList<Vector3>? occupied, float separationSquared)
    {
        if (occupied == null) { return true; }

        for (var i = 0; i < occupied.Count; i++)
        {
            if (candidate.DistanceSquaredTo(occupied[i]) < separationSquared) { return false; }
        }

        return true;
    }
}
