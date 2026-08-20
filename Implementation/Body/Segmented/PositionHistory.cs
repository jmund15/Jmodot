namespace Jmodot.Implementation.Body.Segmented;

using System;
using System.Collections.Generic;
using Godot;

/// <summary>
/// A fixed-capacity trail of the poses a chain head has occupied, sampled by distance travelled
/// rather than by time, so the units riding it keep their spacing at any speed.
/// </summary>
/// <remarks>
/// <para>
/// Samples are ordered front-to-back: index 0 of a <see cref="Reseed"/> batch and the most recent
/// <see cref="TryAppend"/> are the FRONT of the trail, and <see cref="SampleAtDistance"/> measures
/// backwards from there.
/// </para>
/// <para>
/// Sizing and gating are one axis, <c>SamplesPerSpacing</c>: the number of samples recorded per
/// <c>spacing</c> of travelled arc. The append gate is <c>spacing / SamplesPerSpacing</c> and the
/// capacity is <c>(maxSegments + 2) * SamplesPerSpacing</c>. The <c>+ 2</c> is the headroom the two
/// readers past the last unit need — one spacing so an interpolation window exists beyond
/// <c>maxSegments * spacing</c> instead of clamping, and one so a <see cref="Reseed"/> cannot
/// immediately evict the poses it just wrote.
/// </para>
/// </remarks>
public sealed class PositionHistory
{
    private const int SamplesPerSpacing = 4;

    private readonly (Vector3 Position, Vector3 Facing)[] _samples;
    private readonly float _minAppendDistance;

    // Ring indices: _front is the newest occupied slot, _count the occupied span behind it.
    private int _front = -1;
    private int _count;

    /// <param name="maxSegments">The most units this trail must be able to answer for.</param>
    /// <param name="spacing">Metres between unit centres. Must be positive.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="spacing"/> is not a positive finite number, or <paramref name="maxSegments"/> is below one.
    /// </exception>
    public PositionHistory(int maxSegments, float spacing)
    {
        if (maxSegments < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maxSegments), maxSegments, "A trail must carry at least one unit.");
        }

        if (!(spacing > 0f) || !float.IsFinite(spacing))
        {
            throw new ArgumentOutOfRangeException(nameof(spacing), spacing, "Spacing must be a positive finite distance.");
        }

        this._samples = new (Vector3, Vector3)[(maxSegments + 2) * SamplesPerSpacing];
        this._minAppendDistance = spacing / SamplesPerSpacing;
    }

    /// <summary>How many samples the trail can hold before appending evicts the oldest.</summary>
    public int Capacity => this._samples.Length;

    /// <summary>How many samples the trail currently holds.</summary>
    public int Count => this._count;

    /// <summary>
    /// Records <paramref name="position"/> as the new front of the trail when it is at least one
    /// gate distance from the current front.
    /// </summary>
    /// <returns>False when the sample was too close to the front and nothing was recorded.</returns>
    public bool TryAppend(Vector3 position, Vector3 facing)
    {
        if (this._count > 0 && position.DistanceTo(this._samples[this._front].Position) < this._minAppendDistance)
        {
            return false;
        }

        this.Push(position, facing);
        return true;
    }

    /// <summary>
    /// Replaces the whole trail with <paramref name="poses"/>, front first, bypassing the append
    /// gate. The caller owns the spacing of what it writes; this is how a layout, an adoption or a
    /// teleport states the trail's shape outright instead of accumulating it.
    /// </summary>
    public void Reseed(IReadOnlyList<(Vector3 position, Vector3 facing)> poses)
    {
        this._front = -1;
        this._count = 0;
        if (poses == null) { return; }

        // Back-to-front, so the caller's index 0 ends up as the newest sample.
        for (var i = poses.Count - 1; i >= 0; i--)
        {
            this.Push(poses[i].position, poses[i].facing);
        }
    }

    /// <summary>
    /// The pose <paramref name="distanceBack"/> metres back along the recorded arc, interpolated
    /// between the two samples that straddle it. Clamps to the oldest sample once
    /// <paramref name="distanceBack"/> exceeds the recorded arc, and to the front for a
    /// non-positive distance.
    /// </summary>
    /// <exception cref="InvalidOperationException">The trail holds no samples.</exception>
    public (Vector3 position, Vector3 facing) SampleAtDistance(float distanceBack)
    {
        if (this._count == 0)
        {
            throw new InvalidOperationException("An empty PositionHistory has no pose to sample.");
        }

        var front = this._samples[this._front];
        if (!(distanceBack > 0f)) { return (front.Position, front.Facing); }

        var travelled = 0f;
        var newer = front;
        for (var back = 1; back < this._count; back++)
        {
            var older = this._samples[this.SlotBehind(back)];
            var span = newer.Position.DistanceTo(older.Position);
            if (travelled + span >= distanceBack)
            {
                var t = span > 0f ? (distanceBack - travelled) / span : 0f;
                return (newer.Position.Lerp(older.Position, t), Blend(newer.Facing, older.Facing, t));
            }

            travelled += span;
            newer = older;
        }

        return (newer.Position, newer.Facing);
    }

    private void Push(Vector3 position, Vector3 facing)
    {
        this._front = this._front < 0 ? 0 : (this._front + 1) % this._samples.Length;
        this._samples[this._front] = (position, facing);
        if (this._count < this._samples.Length) { this._count++; }
    }

    private int SlotBehind(int stepsBack)
        => ((this._front - stepsBack) % this._samples.Length + this._samples.Length) % this._samples.Length;

    private static Vector3 Blend(Vector3 newer, Vector3 older, float t)
    {
        var blended = newer.Lerp(older, t);
        // Opposed facings cancel at the midpoint; the newer sample is the one a follower is heading toward.
        return blended.IsZeroApprox() ? newer : blended.Normalized();
    }
}
