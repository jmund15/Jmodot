namespace Jmodot.Core.Visual.Effects;

using System.Collections.Generic;
using Godot;

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
