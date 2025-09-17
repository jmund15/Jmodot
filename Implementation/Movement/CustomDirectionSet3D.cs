namespace Jmodot.Implementation.Movement;

using System.Linq;
using Core.Movement;
using Godot.Collections;

/// <summary>
///     Specialized DirectionSet3D that allows for custom direction vectors to be defined in the Godot Editor.
/// </summary>
[GlobalClass]
public sealed partial class CustomDirectionSet3D : DirectionSet3D
{
    private Array<Vector3> _customDirections = new();

    public CustomDirectionSet3D()
    {
        // Default constructor initializes with an empty set
        this.Directions = this.CustomDirections;
    }

    // should keep? or make disctinct
    public CustomDirectionSet3D(Array<Vector3> directions)
    {
        this.Directions = directions;
    }

    [Export]
    private Array<Vector3> CustomDirections
    {
        get => this._customDirections;
        set
        {
            // Ensure all directions are normalized
            this._customDirections = new Array<Vector3>(value.Select(dir => dir.Normalized()));
            this.Directions = this.CustomDirections; // Update the base Directions property
        }
    }
}
