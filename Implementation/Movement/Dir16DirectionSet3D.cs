namespace Jmodot.Implementation.Movement;

using Core.Movement;
using Godot.Collections;

/// <summary>
/// A pre-configured DirectionSet3D for 16 directions at 22.5° intervals on the XZ plane.
/// Provides finer angular resolution than Dir8DirectionSet3D for smoother steering.
/// </summary>
[GlobalClass, Tool]
public sealed partial class Dir16DirectionSet3D : DirectionSet3D
{
    public Dir16DirectionSet3D()
    {
        var dirs = new Array<Vector3>();
        for (int i = 0; i < 16; i++)
        {
            float angle = Mathf.DegToRad(i * 22.5f);
            dirs.Add(new Vector3(Mathf.Sin(angle), 0, Mathf.Cos(angle)));
        }
        Directions = dirs;
    }
}
