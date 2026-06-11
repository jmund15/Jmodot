namespace Jmodot.Implementation.ProcGen.Spatial;

using System.Collections.Generic;
using Godot;

/// <summary>
///     Cell-region occupancy for the embed search: insertion-ordered axis-aligned integer boxes
///     (design-se §4 pinned enumeration discipline — insertion-ordered occupancy). Boxes are
///     half-open on every axis, so face-adjacent rooms share a boundary plane but never a cell.
///     Linear scan is v1-sufficient; spatial acceleration is a recorded lever, not a need.
/// </summary>
internal sealed class OccupancyIndex
{
    private readonly List<(StringName Id, Vector3I Origin, Vector3I Size)> _boxes = new();

    public bool Overlaps(Vector3I origin, Vector3I size)
    {
        foreach ((StringName _, Vector3I existingOrigin, Vector3I existingSize) in this._boxes)
        {
            if (BoxesIntersect(existingOrigin, existingSize, origin, size))
            {
                return true;
            }
        }

        return false;
    }

    public void Add(StringName id, Vector3I origin, Vector3I size)
    {
        this._boxes.Add((id, origin, size));
    }

    public void Remove(StringName id)
    {
        this._boxes.RemoveAll(b => b.Id == id);
    }

    private static bool BoxesIntersect(Vector3I aOrigin, Vector3I aSize, Vector3I bOrigin, Vector3I bSize)
    {
        bool xOverlap = aOrigin.X < bOrigin.X + bSize.X && bOrigin.X < aOrigin.X + aSize.X;
        bool yOverlap = aOrigin.Y < bOrigin.Y + bSize.Y && bOrigin.Y < aOrigin.Y + aSize.Y;
        bool zOverlap = aOrigin.Z < bOrigin.Z + bSize.Z && bOrigin.Z < aOrigin.Z + aSize.Z;
        return xOverlap && yOverlap && zOverlap;
    }
}
