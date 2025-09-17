#region

using System.Linq;
using Godot.Collections;
using Jmodot.Core.Movement;

#endregion

namespace Jmodot.Implementation.Movement;

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
        Directions = CustomDirections;
    }

    // should keep? or make disctinct
    public CustomDirectionSet3D(Array<Vector3> directions)
    {
        Directions = directions;
    }

    [Export]
    private Array<Vector3> CustomDirections
    {
        get => _customDirections;
        set
        {
            // Ensure all directions are normalized
            _customDirections = new Array<Vector3>(value.Select(dir => dir.Normalized()));
            Directions = CustomDirections; // Update the base Directions property
        }
    }
}