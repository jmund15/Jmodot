namespace Jmodot.Implementation.Movement;

using Core.Movement;
using Godot.Collections;

/// <summary>
/// A pre-configured DirectionSet3D for 8 directions (cardinal and inter-cardinal).
/// </summary>
[GlobalClass, Tool]
public sealed partial class Dir8DirectionSet3D : DirectionSet3D
{
    public Dir8DirectionSet3D() { Directions = new Array<Vector3> {
        new Vector3(0, 0, 1).Normalized(), new Vector3(1, 0, 1).Normalized(), new Vector3(1, 0, 0).Normalized(), new Vector3(1, 0, -1).Normalized(),
        new Vector3(0, 0, -1).Normalized(), new Vector3(-1, 0, -1).Normalized(), new Vector3(-1, 0, 0).Normalized(), new Vector3(-1, 0, 1).Normalized()
    }; }
}
