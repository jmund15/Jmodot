namespace Jmodot.Implementation.Movement;

using Core.Movement;
using Godot.Collections;

/// <summary>
/// A pre-configured DirectionSet2D for 4 cardinal directions.
/// </summary>
[GlobalClass, Tool]
public sealed partial class Dir4DirectionSet2D : DirectionSet2D
{
    public Dir4DirectionSet2D() { Directions = new Array<Vector2> {
        new Vector2(0, 1).Normalized(), new Vector2(1,0).Normalized(),
        new Vector2(0,-1).Normalized(), new Vector2(-1,0).Normalized()
    }; }
}
