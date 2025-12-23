namespace Jmodot.Implementation.Movement;

using Core.Movement;
using Godot.Collections;

/// <summary>
/// A pre-configured DirectionSet2D for 8 directions (cardinal and inter-cardinal).
/// </summary>
[GlobalClass, Tool]
public sealed partial class Dir8DirectionSet2D : DirectionSet2D
{
    public Dir8DirectionSet2D() { Directions = new Array<Vector2> {
        new Vector2(0, 1).Normalized(), new Vector2(1,1).Normalized(), new Vector2(1,0).Normalized(), new Vector2(1,-1).Normalized(),
        new Vector2(0,-1).Normalized(), new Vector2(-1,-1).Normalized(), new Vector2(-1,0).Normalized(), new Vector2(-1,1).Normalized()
    }; }
}
