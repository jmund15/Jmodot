namespace Jmodot.Core.Visual.Effects;

using System.Collections.Generic;
using Godot;

/// <summary>
/// Abstract base class for visual effects that can be applied to sprites via Tweens.
/// Subclasses define specific effect behaviors (flash, tint, shader effects, etc.)
/// </summary>
[GlobalClass]
public abstract partial class VisualEffect : Resource
{
    /// <summary>
    /// Total duration of the effect in seconds.
    /// </summary>
    [Export] public float Duration { get; set; } = 1.0f;

    /// <summary>
    /// Priority for effect override. Higher priority effects replace lower priority ones.
    /// Effects with equal priority: the newer one wins.
    /// </summary>
    [Export] public int Priority { get; set; } = 0;

    /// <summary>
    /// Capture the current visual state of the node (e.g., Modulate color) for later restoration.
    /// </summary>
    /// <param name="node">The visual node (Sprite3D, Sprite2D, etc.)</param>
    /// <returns>Dictionary of property names to their original values</returns>
    public abstract Dictionary<string, Variant> CaptureState(Node node);

    /// <summary>
    /// Restore the node to its original visual state.
    /// </summary>
    /// <param name="node">The visual node</param>
    /// <param name="state">The state captured by CaptureState</param>
    public abstract void RestoreState(Node node, Dictionary<string, Variant> state);

    /// <summary>
    /// Configure the Tween to perform this effect on a single node.
    /// The tween is already created; add your TweenProperty/TweenCallback calls.
    /// </summary>
    /// <param name="tween">The Tween to configure</param>
    /// <param name="nodes">The nodes to apply the effect to</param>
    /// <param name="elapsedTime">How far in the tween should start at. Used when the tween needs to "switch" nodes mid-run (e.g. player moves from idle sprite to run sprite)</param>
    public abstract void ConfigureTween(Tween tween, List<Node> nodes, float elapsedTime = 0f);

    #region Helper Methods for Subclasses

    /// <summary>
    /// Get the Modulate color from any supported visual node type.
    /// </summary>
    protected static Color GetModulate(Node node)
    {
        return node switch
        {
            SpriteBase3D sprite3D => sprite3D.Modulate,
            CanvasItem canvasItem => canvasItem.Modulate,
            _ => Colors.White
        };
    }

    /// <summary>
    /// Set the Modulate color on any supported visual node type.
    /// </summary>
    protected static void SetModulate(Node node, Color color)
    {
        if (node is SpriteBase3D sprite3D)
        {
            sprite3D.Modulate = color;
        }
        else if (node is CanvasItem canvasItem)
        {
            canvasItem.Modulate = color;
        }
    }

    /// <summary>
    /// Check if a node is a supported visual type.
    /// </summary>
    public static bool IsVisualNode(Node node)
    {
        return node is SpriteBase3D or CanvasItem;
    }

    #endregion
}
