namespace Jmodot.Core.Visual.Animation.Sprite;

using Godot;
using System.Collections.Generic;

/// <summary>
/// An abstract Resource that defines a strategy for combining a base animation name
/// with a set of variants (like direction, style, etc.) to produce a final, playable animation name.
/// This decouples the naming logic from the animation components entirely.
/// </summary>
[GlobalClass]
public abstract partial class AnimationNamingConvention : Resource
{
    /// <summary>
    /// Combines a base name and variants into a final animation name.
    /// </summary>
    /// <param name="baseName">The core animation name (e.g., "run", "attack").</param>
    /// <param name="variants">An ordered list of variant strings (e.g., ["N", "sword"]).</param>
    /// <returns>The final animation name to be played (e.g., "run_N_sword").</returns>
    public abstract StringName GetFullAnimationName(StringName baseName, IEnumerable<string> variants);
}
