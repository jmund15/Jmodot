namespace Jmodot.Implementation.Visual.Effects;

using System.Collections.Generic;
using Godot;

/// <summary>
/// Tracks intended base modulation colors for visual nodes.
/// Separates "what color should this sprite be" from "what color is it currently displaying".
///
/// Use this component when you need to manage base colors that can be changed at runtime
/// (e.g., equipment with color tints) while still supporting transient visual effects
/// (flash, damage tint, etc.) that modify the visual temporarily.
///
/// <para><b>Usage Flow:</b></para>
/// <list type="number">
///   <item>Equipment system calls <see cref="RegisterBaseColor"/> when applying a tint</item>
///   <item><see cref="VisualEffectController"/> queries <see cref="GetBaseColor"/> instead of reading .Modulate</item>
///   <item>Effects apply ON TOP of the registered base color</item>
///   <item>When equipment changes, call <see cref="UnregisterSprite"/> then <see cref="RegisterBaseColor"/> with new color</item>
/// </list>
/// </summary>
public class BaseModulationTracker
{
    private readonly Dictionary<Node, Color> _baseColors = new();

    /// <summary>
    /// Register the intended base color for a visual node.
    /// This is the color the node "should" be, before any transient effects.
    /// </summary>
    /// <param name="node">The visual node (Sprite2D, SpriteBase3D, etc.)</param>
    /// <param name="baseColor">The intended base modulation color</param>
    public void RegisterBaseColor(Node node, Color baseColor)
    {
        _baseColors[node] = baseColor;
    }

    /// <summary>
    /// Unregister a visual node when it's removed (e.g., unequipped).
    /// </summary>
    /// <param name="node">The node to unregister</param>
    public void UnregisterSprite(Node node)
    {
        _baseColors.Remove(node);
    }

    /// <summary>
    /// Get the registered base color for a node.
    /// Returns <see cref="Colors.White"/> if not registered (no modification).
    /// </summary>
    /// <param name="node">The visual node</param>
    /// <returns>The registered base color, or White if not registered</returns>
    public Color GetBaseColor(Node node)
    {
        return _baseColors.TryGetValue(node, out var color) ? color : Colors.White;
    }

    /// <summary>
    /// Try to get the registered base color for a node.
    /// Returns false if the node is not registered.
    /// </summary>
    /// <param name="node">The visual node</param>
    /// <param name="baseColor">The registered base color (out parameter)</param>
    /// <returns>True if registered, false otherwise</returns>
    public bool TryGetBaseColor(Node node, out Color baseColor)
    {
        return _baseColors.TryGetValue(node, out baseColor);
    }

    /// <summary>
    /// Clear all registered base colors.
    /// Use when resetting the entire visual system.
    /// </summary>
    public void ClearAll()
    {
        _baseColors.Clear();
    }

    /// <summary>
    /// Check if a node has a registered base color.
    /// </summary>
    /// <param name="node">The visual node</param>
    /// <returns>True if the node has a registered base color</returns>
    public bool IsRegistered(Node node)
    {
        return _baseColors.ContainsKey(node);
    }
}
