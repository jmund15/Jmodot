namespace Jmodot.Core.Movement;

using System.Collections.Generic;
using System.Linq;
using Implementation.Shared;

/// <summary>
///     A data-driven Resource that defines a discrete set of directional vectors.
///     This allows game-specific directional models (e.g., 4-way, 8-way) to be created
///     in the editor and used by systems to interpret a continuous direction vector.
/// </summary>
[GlobalClass, Tool]
public abstract partial class DirectionSet3D : Resource
{
    private IEnumerable<Vector3> _directions = Enumerable.Empty<Vector3>();
    private IReadOnlyList<Vector3>? _orderedCache;
    private bool _hasCircularOrder;

    /// <summary>
    /// The collection of normalized direction vectors that make up this set.
    /// This property is populated by the concrete implementations. Reassigning it
    /// invalidates the lazily-built <see cref="OrderedDirections"/> ring.
    /// </summary>
    public IEnumerable<Vector3> Directions
    {
        get => this._directions;
        protected set
        {
            this._directions = value;
            this._orderedCache = null;
        }
    }

    /// <summary>
    /// The directions arranged as a circular ring, angle-sorted counter-clockwise by
    /// Atan2(x, z) normalized to [0, Tau). Lazily built and cached; invalidated when
    /// <see cref="Directions"/> is reassigned. When <see cref="HasCircularOrder"/> is false
    /// (non-planar or fewer than three directions) this returns the direction values in their
    /// original order — neighbor queries then degrade to no-ops at the consumer's gate.
    /// </summary>
    public IReadOnlyList<Vector3> OrderedDirections
    {
        get
        {
            this.EnsureOrdered();
            return this._orderedCache!;
        }
    }

    /// <summary>
    /// True when the set forms a planar XZ ring: three or more directions, all with a
    /// negligible vertical (Y) component. False sets degrade neighbor-dependent features to no-ops.
    /// </summary>
    public bool HasCircularOrder
    {
        get
        {
            this.EnsureOrdered();
            return this._hasCircularOrder;
        }
    }

    /// <summary>
    /// Index of <paramref name="direction"/> within <see cref="OrderedDirections"/>, or -1 if absent.
    /// Matching is approximate (Vector3.IsEqualApprox) to guard float drift on normalized authored dirs.
    /// </summary>
    public int IndexOfOrdered(Vector3 direction)
    {
        var ordered = this.OrderedDirections;
        for (int i = 0; i < ordered.Count; i++)
        {
            if (ordered[i].IsEqualApprox(direction))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>
    /// The direction <paramref name="offset"/> steps away from <paramref name="direction"/> in the
    /// circular ring (negative walks the opposite way; indices wrap). Returns Vector3.Zero when the
    /// direction is absent or the set is empty.
    /// </summary>
    public Vector3 GetOrderedNeighbor(Vector3 direction, int offset)
    {
        var ordered = this.OrderedDirections;
        int count = ordered.Count;
        if (count == 0)
        {
            return Vector3.Zero;
        }

        int index = this.IndexOfOrdered(direction);
        if (index < 0)
        {
            return Vector3.Zero;
        }

        int wrapped = ((index + offset) % count + count) % count;
        return ordered[wrapped];
    }

    /// <summary>
    /// Sorts the existing direction VALUES into the circular ring — it never re-normalizes or
    /// reconstructs the vectors, so exact-value dictionary keys downstream (SteeringPropagation)
    /// stay intact. No-ops after the first build until Directions is reassigned.
    /// </summary>
    private void EnsureOrdered()
    {
        if (this._orderedCache != null)
        {
            return;
        }

        var list = this._directions.ToList();
        this._hasCircularOrder = list.Count >= 3 && list.All(d => Mathf.Abs(d.Y) < 1e-4f);

        if (this._hasCircularOrder)
        {
            list.Sort((a, b) => NormalizedAngle(a).CompareTo(NormalizedAngle(b)));
        }

        this._orderedCache = list;
    }

    // Construction angle in [0, Tau) from an XZ-plane direction. Atan2(x, z) alone lives in
    // (-pi, pi]; shifting negatives by Tau reproduces the authored 0..360-degree sweep, so the
    // ring matches the theta-ascending build order of Dir8/Dir16 (legacy propagation parity).
    private static float NormalizedAngle(Vector3 d)
    {
        float angle = Mathf.Atan2(d.X, d.Z);
        return angle < 0f ? angle + Mathf.Tau : angle;
    }

    /// <summary>
    /// Finds the closest direction vector in this set to a given target direction.
    /// This is the core logic that snaps a continuous input (like from a joystick)
    /// to a discrete direction.
    /// </summary>
    /// <param name="targetDirection">The continuous, normalized direction to check against.</param>
    /// <returns>The closest matching Vector3 from the Directions collection, or Vector3.Zero if none found.</returns>
    public Vector3 GetClosestDirection(Vector3 targetDirection)
    {
        if (targetDirection.LengthSquared() < 1e-6f)
        {
            return Vector3.Zero;
        }

        Vector3? closestDir = null;
        var maxDot = float.MinValue;
        var normalizedTarget = targetDirection.Normalized();

        // The dot product of two normalized vectors gives the cosine of the angle between them.
        // A higher dot product means a smaller angle, so we are looking for the maximum dot product.
        foreach (var dir in this.Directions)
        {
            var dot = dir.Dot(normalizedTarget);
            if (dot > maxDot)
            {
                maxDot = dot;
                closestDir = dir;
            }
        }

        if (closestDir == null)
        {
            JmoLogger.Error(this,
                $"No valid direction found for {targetDirection} within the DirectionSet3D '{this.ResourceName}'.");
        }

        return closestDir ?? Vector3.Zero;
    }
}
