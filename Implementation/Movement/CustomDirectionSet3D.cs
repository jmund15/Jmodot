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
        this.Directions = UniqueUnitDirections(directions);
    }

    [Export]
    private Array<Vector3> CustomDirections
    {
        get => this._customDirections;
        set
        {
            this._customDirections = UniqueUnitDirections(value);
            this.Directions = this.CustomDirections;
        }
    }

    // Consumers key per-direction scores by the vector, so two entries normalizing to one would collide.
    private static Array<Vector3> UniqueUnitDirections(Array<Vector3> directions)
        => new(directions.Where(dir => dir.LengthSquared() >= 1e-6f).Select(dir => dir.Normalized()).Distinct());
}
