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
public abstract partial class DirectionSet2D : Resource
{
    private IEnumerable<Vector2> _directions = Enumerable.Empty<Vector2>();
    private IReadOnlyList<Vector2>? _orderedCache;
    private bool _hasCircularOrder;

    /// <summary>
    /// The collection of normalized direction vectors that make up this set.
    /// This property is populated by the concrete implementations. Reassigning it
    /// invalidates the lazily-built <see cref="OrderedDirections"/> ring.
    /// </summary>
    public IEnumerable<Vector2> Directions
    {
        get => this._directions;
        protected set
        {
            this._directions = value;
            this._orderedCache = null;
        }
    }

    /// <summary>
    /// The directions arranged as a circular ring, angle-sorted by Atan2(y, x) ascending.
    /// Lazily built and cached; invalidated when <see cref="Directions"/> is reassigned. When
    /// <see cref="HasCircularOrder"/> is false (fewer than three directions) this returns the
    /// direction values in their original order — neighbor queries then degrade to no-ops at the
    /// consumer's gate.
    /// </summary>
    public IReadOnlyList<Vector2> OrderedDirections
    {
        get
        {
            this.EnsureOrdered();
            return this._orderedCache!;
        }
    }

    /// <summary>
    /// True when the set has three or more directions. 2D has no vertical concept, so a planar
    /// ring of three or more is always circular. False sets degrade neighbor-dependent features to no-ops.
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
    /// Matching is approximate (Vector2.IsEqualApprox) to guard float drift on normalized authored dirs.
    /// </summary>
    public int IndexOfOrdered(Vector2 direction)
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
    /// circular ring (negative walks the opposite way; indices wrap). Returns Vector2.Zero when the
    /// direction is absent or the set is empty.
    /// </summary>
    public Vector2 GetOrderedNeighbor(Vector2 direction, int offset)
    {
        var ordered = this.OrderedDirections;
        int count = ordered.Count;
        if (count == 0)
        {
            return Vector2.Zero;
        }

        int index = this.IndexOfOrdered(direction);
        if (index < 0)
        {
            return Vector2.Zero;
        }

        int wrapped = ((index + offset) % count + count) % count;
        return ordered[wrapped];
    }

    /// <summary>
    /// Sorts the existing direction VALUES into the circular ring — it never re-normalizes or
    /// reconstructs the vectors. No-ops after the first build until Directions is reassigned.
    /// 2D has no legacy-propagation consumer, so Atan2(y, x) is used directly (no [0, Tau)
    /// authored-order reproduction is required, unlike the 3D mirror).
    /// </summary>
    private void EnsureOrdered()
    {
        if (this._orderedCache != null)
        {
            return;
        }

        var list = this._directions.ToList();
        this._hasCircularOrder = list.Count >= 3;

        if (this._hasCircularOrder)
        {
            list.Sort((a, b) => Mathf.Atan2(a.Y, a.X).CompareTo(Mathf.Atan2(b.Y, b.X)));
        }

        this._orderedCache = list;
    }

    /// <summary>
    /// Finds the closest direction vector in this set to a given target direction.
    /// This is the core logic that snaps a continuous input (like from a joystick)
    /// to a discrete direction.
    /// </summary>
    /// <param name="targetDirection">The continuous, normalized direction to check against.</param>
    /// <returns>The closest matching Vector2 from the Directions collection, or Vector2.Zero if none found.</returns>
    public Vector2 GetClosestDirection(Vector2 targetDirection)
    {
        if (targetDirection.LengthSquared() < 1e-6f)
        {
            return Vector2.Zero;
        }

        Vector2? closestDir = null;
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
                $"No valid direction found for {targetDirection} within the DirectionSet2D '{this.ResourceName}'.");
        }

        return closestDir ?? Vector2.Zero;
    }
}
