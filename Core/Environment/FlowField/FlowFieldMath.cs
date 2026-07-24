namespace Jmodot.Core.Environment.FlowField;

using System;
using System.Collections.Generic;

/// <summary>
///     Pure static math for sparse flow fields: accumulation, decay, sampling, merge eligibility and
///     bounds. Host-agnostic by construction — no scene tree, no Resources, no engine calls, so the whole
///     surface is exercisable in isolation.
/// </summary>
/// <remarks>
///     Curve-shaped inputs arrive as pre-baked <c>float[]</c> tables rather than Godot <c>Curve</c>
///     instances. A table of N samples spans a normalized domain of [0, 1] with linear interpolation
///     between adjacent samples, so <c>[0f, 1f]</c> is the identity ramp.
/// </remarks>
public static class FlowFieldMath
{
    /// <summary>Floor on the inverse-distance blend weight, so a sample exactly on a spine stays finite.</summary>
    private const float MinBlendDistance = 0.0001f;

    /// <summary>
    ///     Index of the segment a deposit at <paramref name="pos" /> heading <paramref name="dir" /> should
    ///     reinforce: the NEAREST segment within <paramref name="mergeRadius" /> whose direction agrees to at
    ///     least <paramref name="alignmentDotMin" />. Returns -1 when nothing qualifies, which the caller
    ///     reads as "start a new segment".
    /// </summary>
    /// <remarks>
    ///     A zero-length <paramref name="dir" /> normalizes to zero and therefore always fails the alignment
    ///     gate — a deposit with no heading can never silently claim a segment.
    /// </remarks>
    public static int FindReinforceTarget(IReadOnlyList<FlowSegment> segments, Vector3 pos, Vector3 dir,
        float mergeRadius, float alignmentDotMin)
    {
        var depositDir = dir.Normalized();
        var bestIndex = -1;
        var bestDistance = float.MaxValue;

        for (var i = 0; i < segments.Count; i++)
        {
            var segment = segments[i];
            var distance = segment.Position.DistanceTo(pos);
            if (distance > mergeRadius || distance >= bestDistance)
            {
                continue;
            }

            if (segment.Direction.Normalized().Dot(depositDir) < alignmentDotMin)
            {
                continue;
            }

            bestIndex = i;
            bestDistance = distance;
        }

        return bestIndex;
    }

    /// <summary>
    ///     Folds a deposit into an existing segment: energy accumulates up to <paramref name="maxEnergy" />,
    ///     direction becomes an energy-weighted mean. Position and radius are preserved.
    /// </summary>
    /// <remarks>
    ///     The blend weight is the segment's LIVE energy, not its cap, so a nearly-decayed segment is
    ///     re-steered by a fresh deposit while a saturated one barely budges — stale directional bias fades
    ///     out on its own as energy drains.
    /// </remarks>
    public static FlowSegment Reinforce(FlowSegment target, Vector3 depositDir, float depositEnergy,
        float maxEnergy)
    {
        var blended = (target.Direction.Normalized() * target.Energy) + (depositDir.Normalized() * depositEnergy);
        var direction = blended.LengthSquared() > 0f ? blended.Normalized() : target.Direction;

        return new FlowSegment
        {
            Position = target.Position,
            Direction = direction,
            Radius = target.Radius,
            Energy = MathF.Min(target.Energy + depositEnergy, maxEnergy),
        };
    }

    /// <summary>Linear decay fallback. Never returns a negative energy.</summary>
    public static float Decay(float energy, float decayPerSecond, float deltaSeconds)
    {
        var decayed = energy - (decayPerSecond * deltaSeconds);
        return decayed > 0f ? decayed : 0f;
    }

    /// <summary>
    ///     Curve-shaped decay. The baked table maps normalized energy to a normalized rate, which scales by
    ///     <paramref name="maxEnergy" /> to give energy-per-second — so a table value of 1 drains a full
    ///     segment in one second at any cap. Never returns a negative energy.
    /// </summary>
    public static float DecayCurved(float energy, float maxEnergy, float[] decayRate01ByEnergy01,
        float deltaSeconds)
    {
        if (maxEnergy <= 0f)
        {
            return energy > 0f ? energy : 0f;
        }

        var energy01 = Math.Clamp(energy / maxEnergy, 0f, 1f);
        var rate01 = SampleTable01(decayRate01ByEnergy01, energy01);

        return Decay(energy, rate01 * maxEnergy, deltaSeconds);
    }

    /// <summary>
    ///     Field velocity at <paramref name="worldPos" />: an inverse-distance blend of the two nearest
    ///     segments whose influence radius covers the point. <see cref="Vector3.Zero" /> outside all radii.
    /// </summary>
    /// <remarks>
    ///     Two rather than one, because nearest-only sampling pops direction as a target crosses a segment
    ///     boundary; two is the cheapest blend that stays continuous along the spine.
    /// </remarks>
    public static Vector3 SampleVelocity(IReadOnlyList<FlowSegment> segments, Vector3 worldPos, float maxEnergy,
        float[] radialFalloff01, float[] energyToSpeed01)
    {
        var nearestIndex = -1;
        var secondIndex = -1;
        var nearestDistance = float.MaxValue;
        var secondDistance = float.MaxValue;

        for (var i = 0; i < segments.Count; i++)
        {
            var segment = segments[i];
            if (segment.Radius <= 0f)
            {
                continue;
            }

            var distance = segment.Position.DistanceTo(worldPos);
            if (distance > segment.Radius)
            {
                continue;
            }

            if (distance < nearestDistance)
            {
                secondIndex = nearestIndex;
                secondDistance = nearestDistance;
                nearestIndex = i;
                nearestDistance = distance;
                continue;
            }

            if (distance < secondDistance)
            {
                secondIndex = i;
                secondDistance = distance;
            }
        }

        if (nearestIndex < 0)
        {
            return Vector3.Zero;
        }

        var totalWeight = 1f / MathF.Max(nearestDistance, MinBlendDistance);
        var weighted = SegmentVelocity(segments[nearestIndex], nearestDistance, maxEnergy, radialFalloff01,
            energyToSpeed01) * totalWeight;

        if (secondIndex >= 0)
        {
            var secondWeight = 1f / MathF.Max(secondDistance, MinBlendDistance);
            weighted += SegmentVelocity(segments[secondIndex], secondDistance, maxEnergy, radialFalloff01,
                energyToSpeed01) * secondWeight;
            totalWeight += secondWeight;
        }

        return weighted / totalWeight;
    }

    /// <summary>
    ///     Whether two flow-field entities may be collapsed into one: some segment pair must sit within
    ///     <paramref name="mergeRadius" /> and agree in direction to at least
    ///     <paramref name="alignmentDotMin" />, AND both must carry the SAME profile instance.
    /// </summary>
    /// <remarks>
    ///     The profile check is the whole reason this takes entities rather than segment lists. Absorbing
    ///     across profiles would corrupt profile-keyed identity and visuals, and splitting the gate between
    ///     here and the calling manager would make it possible to satisfy one half and forget the other. An
    ///     absent profile fails closed — reference equality of two nulls must not read as shared identity.
    /// </remarks>
    public static bool AreEntitiesMergeable(FlowFieldEntity a, FlowFieldEntity b,
        float mergeRadius, float alignmentDotMin)
    {
        if (a.Profile is null || b.Profile is null || !ReferenceEquals(a.Profile, b.Profile))
        {
            return false;
        }

        foreach (var segmentA in a.Segments)
        {
            var directionA = segmentA.Direction.Normalized();
            foreach (var segmentB in b.Segments)
            {
                if (segmentA.Position.DistanceTo(segmentB.Position) > mergeRadius)
                {
                    continue;
                }

                if (directionA.Dot(segmentB.Direction.Normalized()) < alignmentDotMin)
                {
                    continue;
                }

                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     Tightest axis-aligned box containing every segment's influence sphere. An empty chain yields a
    ///     zero-sized box at the origin.
    /// </summary>
    public static Aabb ComputeBounds(IReadOnlyList<FlowSegment> segments)
    {
        if (segments.Count == 0)
        {
            return new Aabb();
        }

        var min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
        var max = new Vector3(float.MinValue, float.MinValue, float.MinValue);

        foreach (var segment in segments)
        {
            var radius = MathF.Max(segment.Radius, 0f);
            min.X = MathF.Min(min.X, segment.Position.X - radius);
            min.Y = MathF.Min(min.Y, segment.Position.Y - radius);
            min.Z = MathF.Min(min.Z, segment.Position.Z - radius);
            max.X = MathF.Max(max.X, segment.Position.X + radius);
            max.Y = MathF.Max(max.Y, segment.Position.Y + radius);
            max.Z = MathF.Max(max.Z, segment.Position.Z + radius);
        }

        return new Aabb(min, max - min);
    }

    /// <summary>
    ///     Whether a physics shape sized to <paramref name="current" /> must be resized to
    ///     <paramref name="candidate" />. Growth is reported the instant the candidate escapes the current
    ///     box padded by <paramref name="growMargin" />; shrinkage only once the candidate falls at least
    ///     <paramref name="shrinkHysteresis" /> (as a volume fraction) below the current box.
    /// </summary>
    /// <remarks>
    ///     The asymmetry is deliberate. Growth must be immediate because force delivery is overlap-gated —
    ///     a stale box means the field is never even asked for a force at its leading edge. Shrinkage is
    ///     pure housekeeping, so it lags to keep broad-phase shape swaps rare.
    /// </remarks>
    public static bool ShouldRebuildBounds(Aabb current, Aabb candidate, float growMargin,
        float shrinkHysteresis)
    {
        var margin = MathF.Max(growMargin, 0f);
        var padding = new Vector3(margin, margin, margin);
        var paddedMin = current.Position - padding;
        var paddedMax = current.Position + current.Size + padding;
        var candidateMin = candidate.Position;
        var candidateMax = candidate.Position + candidate.Size;

        var escapesPadding = candidateMin.X < paddedMin.X || candidateMin.Y < paddedMin.Y
            || candidateMin.Z < paddedMin.Z || candidateMax.X > paddedMax.X || candidateMax.Y > paddedMax.Y
            || candidateMax.Z > paddedMax.Z;
        if (escapesPadding)
        {
            return true;
        }

        var currentVolume = Volume(current);
        if (currentVolume <= 0f)
        {
            return false;
        }

        return Volume(candidate) <= currentVolume * (1f - shrinkHysteresis);
    }

    private static Vector3 SegmentVelocity(FlowSegment segment, float distance, float maxEnergy,
        float[] radialFalloff01, float[] energyToSpeed01)
    {
        var falloff = SampleTable01(radialFalloff01, segment.Radius > 0f ? distance / segment.Radius : 1f);
        var energy01 = maxEnergy > 0f ? Math.Clamp(segment.Energy / maxEnergy, 0f, 1f) : 0f;
        var speed = SampleTable01(energyToSpeed01, energy01);

        return segment.Direction.Normalized() * speed * falloff;
    }

    private static float SampleTable01(float[] table, float t01)
    {
        if (table.Length == 0)
        {
            return 0f;
        }

        if (table.Length == 1)
        {
            return table[0];
        }

        var scaled = Math.Clamp(t01, 0f, 1f) * (table.Length - 1);
        var lower = (int)MathF.Floor(scaled);
        if (lower >= table.Length - 1)
        {
            return table[table.Length - 1];
        }

        return table[lower] + ((table[lower + 1] - table[lower]) * (scaled - lower));
    }

    private static float Volume(Aabb box)
    {
        return MathF.Max(box.Size.X, 0f) * MathF.Max(box.Size.Y, 0f) * MathF.Max(box.Size.Z, 0f);
    }
}
