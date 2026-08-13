namespace Jmodot.Implementation.Interaction.Attachment;

using System;
using System.Collections.Generic;
using Godot;
using Jmodot.Implementation.Shared;
using Jmodot.Implementation.Visual;

/// <summary>
/// The deterministic maths behind scattered placement: a rejection-sampled, entity-local anchor
/// inside the host's measured silhouette that clears every anchor already taken. Pure — no node
/// access, no <see cref="JmoRng"/> field, and no authored tuning of its own; every knob arrives as a
/// parameter from the <see cref="AttachmentAnchorProfile3D"/> that owns it, and randomness arrives
/// as an injected roll delegate so the same roll sequence always produces the same anchor.
/// </summary>
public static class AttachmentAnchorPlacer
{
    /// <summary>
    /// Centre-to-centre distance a rider of <paramref name="footprint"/> needs from every anchor
    /// already taken on <paramref name="bounds"/>. Scales linearly with footprint, so a rider that
    /// occupies twice the capacity also claims twice the room.
    /// </summary>
    public static float MinSeparationFor(
        VisualBounds3D bounds, float footprint, float separationRatioPerFootprintUnit)
        => bounds.Largest * separationRatioPerFootprintUnit * Mathf.Max(footprint, 0f);

    /// <summary>
    /// An entity-local anchor for a rider of <paramref name="footprint"/>, or null when the host's
    /// art could not be measured or no attempt cleared <paramref name="occupied"/>. Offsets are
    /// measured from <see cref="VisualBounds3D.Center"/>.
    /// </summary>
    /// <param name="separationRatioPerFootprintUnit">Fraction of the silhouette's dominant extent one unit of footprint claims.</param>
    /// <param name="maxPlacementAttempts">Rejected candidates before the placer gives up.</param>
    /// <param name="depthRange">Half-range, in metres, the anchor may sit off the sprite plane. 0 keeps Z exactly zero.</param>
    /// <param name="nextUnitFloat">Roll source yielding values in [0, 1). Two rolls are consumed per
    /// attempt (X then Y), plus a third for Z only when <paramref name="depthRange"/> is positive.</param>
    public static Vector3? Place(
        VisualBounds3D bounds,
        IReadOnlyList<Vector3> occupied,
        float footprint,
        Func<float> nextUnitFloat,
        float separationRatioPerFootprintUnit,
        int maxPlacementAttempts,
        float depthRange)
    {
        if (!bounds.IsMeasured) { return null; }

        if (nextUnitFloat == null)
        {
            JmoLogger.Error(typeof(AttachmentAnchorPlacer),
                "Anchor placement requires an injected roll source; none was supplied.");
            return null;
        }

        var separation = MinSeparationFor(bounds, footprint, separationRatioPerFootprintUnit);
        var separationSquared = separation * separation;

        for (var attempt = 0; attempt < maxPlacementAttempts; attempt++)
        {
            var candidate = Sample(bounds, nextUnitFloat, depthRange);
            if (IsClear(candidate, occupied, separationSquared)) { return candidate; }
        }

        return null;
    }

    private static Vector3 Sample(VisualBounds3D bounds, Func<float> nextUnitFloat, float depthRange)
    {
        var x = bounds.Center.X + ((nextUnitFloat() - 0.5f) * bounds.Width);
        var y = bounds.Center.Y + ((nextUnitFloat() - 0.5f) * bounds.Height);
        // The roll is drawn only for a positive range, so a planar profile consumes the exact same
        // sequence it always did — a depth knob nobody turned on cannot shift anyone's anchor.
        var z = depthRange > 0f ? (nextUnitFloat() - 0.5f) * 2f * depthRange : 0f;
        return new Vector3(x, y, z);
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
