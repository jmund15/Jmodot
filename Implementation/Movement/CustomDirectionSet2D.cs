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

    public CustomDirectionSet2D(Array<Vector2> directions)
    {
        this.Directions = UniqueUnitDirections(directions);
    }

    [Export]
    private Array<Vector2> CustomDirections
    {
        get => this._customDirections;
        set
        {
            this._customDirections = UniqueUnitDirections(value);
            this.Directions = this.CustomDirections;
        }
    }

    // Consumers key per-direction scores by the vector, so two entries normalizing to one would collide.
    private static Array<Vector2> UniqueUnitDirections(Array<Vector2> directions)
        => new(directions.Where(dir => dir.LengthSquared() >= 1e-6f).Select(dir => dir.Normalized()).Distinct());
}
