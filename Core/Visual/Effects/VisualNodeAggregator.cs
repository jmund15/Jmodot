namespace Jmodot.Core.Visual.Effects;

using System;
using System.Collections.Generic;
using Godot;
using Jmodot.Core.Visual;

/// <summary>
/// Centralized recursive discovery of visual sprite nodes (SpriteBase3D, Sprite2D).
/// The single source of truth for the fallback sprite scan used by providers that
/// don't publish an explicit visual node registry.
/// </summary>
/// <remarks>
/// Previously duplicated across VisualSlot.FindSpritesRecursive and
/// VisualEffectController.GetAllVisualNodes (non-provider branch). The latter also
/// had a latent bug: it used a type-name string-match for 2D sprites, which would
/// false-match any class whose type name contains "Sprite". This helper performs
/// correct type-based matching via the `is SpriteBase3D or Sprite2D` pattern.
/// </remarks>
public static class VisualNodeAggregator
{
    /// <summary>Returns providers whose ancestor under the root is not also a provider.</summary>
    public static IReadOnlyList<IVisualNodeProvider> CollectProviders(Node root)
    {
        var providers = new List<IVisualNodeProvider>();
        Collect(root, providers);
        return providers;

        static void Collect(Node node, List<IVisualNodeProvider> found)
        {
            if (node is IVisualNodeProvider provider)
            {
                found.Add(provider);
                return;
            }
            foreach (var child in node.GetChildren()) { Collect(child, found); }
        }
    }

    /// <summary>
    /// Appends every sprite node under <paramref name="root"/> (inclusive) to
    /// <paramref name="results"/>. Matches <see cref="SpriteBase3D"/>
    /// (Sprite3D, AnimatedSprite3D) and <see cref="Sprite2D"/>.
    /// </summary>
    public static void CollectSprites(Node root, List<Node> results)
    {
        if (root is SpriteBase3D or Sprite2D)
        {
            results.Add(root);
        }
        foreach (var child in root.GetChildren())
        {
            CollectSprites(child, results);
        }
    }

    /// <summary>
    /// True when <paramref name="node"/> is a <see cref="SpriteBase3D"/> and some <see cref="SpriteBase3D"/> in its unbroken
    /// run of sprite parents (parent, grandparent, … up to the first ancestor that is not a sprite) is accepted by
    /// <paramref name="isTarget"/>. The engine multiplies that ancestor's accumulated colour into this sprite's, so a writer
    /// that sets both applies its colour twice. A sprite under a non-sprite node, or whose whole sprite-parent run lies
    /// outside the writer's set, keeps its own write. Evaluate every member against the writer's whole set before removing
    /// any, so a chain keeps only its top.
    /// </summary>
    public static bool InheritsModulate(Node node, Func<Node, bool> isTarget)
    {
        if (node is not SpriteBase3D) { return false; }

        for (var parent = node.GetParent(); parent is SpriteBase3D; parent = parent.GetParent())
        {
            if (isTarget(parent)) { return true; }
        }
        return false;
    }

    /// <summary>
    /// Returns a new list containing every sprite node under <paramref name="root"/>
    /// (inclusive). Convenience wrapper around the append overload.
    /// </summary>
    public static List<Node> CollectSprites(Node root)
    {
        var results = new List<Node>();
        CollectSprites(root, results);
        return results;
    }
}
