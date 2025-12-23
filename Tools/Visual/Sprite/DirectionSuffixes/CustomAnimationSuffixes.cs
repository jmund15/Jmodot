namespace Jmodot.Tools.Visual.Sprite;

using System.Linq;
using Godot.Collections;

/// <summary>
///     Specialized DirectionSet3D that allows for custom direction vectors to be defined in the Godot Editor.
/// </summary>
[GlobalClass, Tool]
public sealed partial class CustomAnimationSuffixes : AnimationDirectionSuffixes
{
    private Array<string> _customSuffixes = new();

    public CustomAnimationSuffixes()
    {
        // Default constructor initializes with an empty set
        this.DirectionSuffixes = this.CustomSuffixes.ToList();
    }

    // should keep? or make disctinct
    public CustomAnimationSuffixes(Array<string> suffixes)
    {
        this.DirectionSuffixes = suffixes.ToList();
    }

    [Export]
    private Array<string> CustomSuffixes
    {
        get => _customSuffixes;
        set
        { 
            _customSuffixes = value;
            DirectionSuffixes = _customSuffixes.ToList(); // Update the base Directions property
        }
    }
}
