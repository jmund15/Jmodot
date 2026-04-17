namespace Jmodot.Implementation.Movement;

using Core.Movement;
using Godot.Collections;

/// <summary>
///     DirectionSet2D specifically for 4 cardinal directions on the XY plane.
/// </summary>
[GlobalClass]
public sealed partial class Dir4DirectionSet2D : DirectionSet2D
{
    public Dir4DirectionSet2D()
    {
        this.Directions = this.Dir4Set;
    }

    private Array<Vector2> Dir4Set { get; init; } = new()
    {
        new Vector2(1, 0), // Right
        new Vector2(0, 1), // Down
        new Vector2(-1, 0), // Left
        new Vector2(0, -1) // Up
    };
}
