namespace Jmodot.Core.PointCloud;

using System.Collections.Generic;
using Godot;

/// <summary>
/// Spatial hash grid for accelerating nearest-neighbor queries during point cloud generation.
/// Partitions 3D space into uniform cells for O(1) amortized neighbor lookups,
/// replacing the O(n) linear scan in CalculateMinDistance.
/// </summary>
public class SpatialHashGrid3D
{
    private readonly float _inverseCellSize;
    private readonly Dictionary<(int, int, int), List<Vector3>> _cells = new();

    public int Count { get; private set; }

    public SpatialHashGrid3D(float cellSize)
    {
        _inverseCellSize = 1f / cellSize;
    }

    public void Insert(Vector3 point)
    {
        var key = GetCellKey(point);
        if (!_cells.TryGetValue(key, out var list))
        {
            list = new List<Vector3>();
            _cells[key] = list;
        }
        list.Add(point);
        Count++;
    }

    /// <summary>
    /// Returns true if any inserted point is strictly closer than <paramref name="distance"/>.
    /// Uses squared distance comparison (no sqrt) for performance.
    /// Strict less-than matches the Poisson acceptance logic: minDist >= minSpacing -> accept.
    /// </summary>
    public bool HasNeighborWithinDistance(Vector3 point, float distance)
    {
        float distanceSq = distance * distance;
        var (cx, cy, cz) = GetCellKey(point);

        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dz = -1; dz <= 1; dz++)
                {
                    var key = (cx + dx, cy + dy, cz + dz);
                    if (!_cells.TryGetValue(key, out var list)) { continue; }

                    foreach (var existing in list)
                    {
                        if (point.DistanceSquaredTo(existing) < distanceSq)
                        {
                            return true;
                        }
                    }
                }
            }
        }

        return false;
    }

    private (int, int, int) GetCellKey(Vector3 point)
    {
        return (
            (int)Mathf.Floor(point.X * _inverseCellSize),
            (int)Mathf.Floor(point.Y * _inverseCellSize),
            (int)Mathf.Floor(point.Z * _inverseCellSize)
        );
    }
}
