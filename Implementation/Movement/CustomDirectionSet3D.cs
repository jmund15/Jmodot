namespace Jmodot.Implementation.Movement;

using System.Linq;
using Core.Movement;
using Godot.Collections;

/// <summary>
///     Specialized DirectionSet3D that allows for custom direction vectors to be defined in the Godot Editor.
/// </summary>
[GlobalClass, Tool]
public sealed partial class CustomDirectionSet3D : DirectionSet3D
{
    private Array<Vector3> _customDirections = new();

    public CustomDirectionSet3D()
    {
        // Default constructor initializes with an empty set
        this.Directions = this.CustomDirections;
    }

    public CustomDirectionSet3D(Array<Vector3> directions)
    {
        this.Directions = new Array<Vector3>(
            directions.Where(dir => dir.LengthSquared() >= 1e-6f).Select(dir => dir.Normalized()));
    }

    [Export]
    private Array<Vector3> CustomDirections
    {
        get => this._customDirections;
        set
        {
            // Ensure all directions are normalized
            this._customDirections = new Array<Vector3>(
                value.Where(dir => dir.LengthSquared() >= 1e-6f).Select(dir => dir.Normalized()));
            this.Directions = this.CustomDirections; // Update the base Directions property
        }
    }
}
