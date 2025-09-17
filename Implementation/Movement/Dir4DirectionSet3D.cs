#region

using Godot.Collections;
using Jmodot.Core.Movement;

#endregion

namespace Jmodot.Implementation.Movement;

/// <summary>
///     DirectionSet3D specifically for 4 cardinal directions on the XZ plane.
/// </summary>
[GlobalClass]
public sealed partial class Dir4DirectionSet3D : DirectionSet3D
{
    public Dir4DirectionSet3D()
    {
        Directions = Dir4Set;
    }

    private Array<Vector3> Dir4Set { get; init; } = new()
    {
        new Vector3(1, 0, 0), // Right
        new Vector3(0, 0, 1), // Forward
        new Vector3(-1, 0, 0), // Left
        new Vector3(0, 0, -1) // Backward
    };
}