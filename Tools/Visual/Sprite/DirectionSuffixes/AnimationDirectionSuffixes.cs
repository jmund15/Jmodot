namespace Jmodot.Tools.Visual.Sprite;

using System.Collections.Generic;
using Godot;

/// <summary>
/// Defines a template for sprite sheet directions.
/// Maps column indices to direction names.
/// </summary>
[GlobalClass]
public abstract partial class AnimationDirectionSuffixes : Resource
{
    /// <summary>
    /// The list of suffixes to append to the animation name, corresponding to columns in the sprite sheet.
    /// Example: ["down", "right", "up", "left"]
    /// </summary>
    //[Export]
    public List<string> DirectionSuffixes { get; protected set; }
}
