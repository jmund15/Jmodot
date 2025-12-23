namespace Jmodot.Implementation.Movement;

using System.Linq;
using Core.Movement;
using Godot.Collections;

/// <summary>
///     Specialized DirectionSet2D that allows for custom direction vectors to be defined in the Godot Editor.
/// </summary>
[GlobalClass, Tool]
public sealed partial class CustomDirectionSet2D : DirectionSet2D
{
    private Array<Vector2> _customDirections = new();

    public CustomDirectionSet2D()
    {
        // Default constructor initializes with an empty set
        this.Directions = this.CustomDirections;
    }

    // should keep? or make distinct
    public CustomDirectionSet2D(Array<Vector2> directions)
    {
        this.Directions = directions;
    }

    [Export]
    private Array<Vector2> CustomDirections
    {
        get => this._customDirections;
        set
        {
            // Ensure all directions are normalized
            this._customDirections = new Array<Vector2>(value.Select(dir => dir.Normalized()));
            this.Directions = this.CustomDirections; // Update the base Directions property
        }
    }
}
