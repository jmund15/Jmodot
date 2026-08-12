namespace Jmodot.Implementation.Visual.Sprite;

using System.Collections.Generic;
using Godot;
using Jmodot.Core.Visual.Effects;
using GCol = Godot.Collections;

/// <summary>
/// How a derived sprite set treats sprites nested under other sprites.
/// </summary>
public enum SpriteTargetScope
{
    /// <summary>
    /// Every sprite under the root, nesting included. Correct for writers whose property does
    /// NOT inherit down the transform chain — <c>Modulate</c>, <c>FlipH</c>.
    /// </summary>
    EntireSubtree,

    /// <summary>
    /// Only sprites with no sprite ancestor under the same root. Required for writers of
    /// <see cref="Node3D.Scale"/>, which compounds multiplicatively: a nested sprite would be
    /// scaled once by its own writer and again by its ancestor's, deforming twice as hard.
    /// </summary>
    TopMost,
}

/// <summary>
/// Resolves the <see cref="SpriteBase3D"/> nodes a visual component acts on — from either an
/// author's explicit list or a scan under a root — and reports what an author got wrong.
/// </summary>
/// <remarks>
/// <para>
/// <b>Resolution is all-or-nothing.</b> An explicit list with an empty or wrong-typed slot
/// resolves to NOTHING and returns false, rather than to the entries that happened to be valid.
/// A partially-resolved visual set is indistinguishable at runtime from a working one — the
/// component keeps animating, just not everywhere — which is precisely the failure this type
/// exists to make loud.
/// </para>
/// <para>
/// <b>Call once at initialization.</b> <see cref="Resolved"/> is empty until a
/// <c>TryResolve*</c> call returns true, and each call replaces the previous result. Consumers
/// hold the instance for the node's lifetime and re-resolve only if the entity is rebound
/// (pool reuse, reparent).
/// </para>
/// <para>
/// <b>The set does not own liveness.</b> Sprites can be freed independently of the component
/// holding this set, so every write through <see cref="Resolved"/> is the caller's
/// responsibility to guard with <see cref="GodotObject.IsInstanceValid"/>.
/// </para>
/// </remarks>
public sealed class SpriteTargetSet3D
{
    private readonly List<SpriteBase3D> _resolved = new();

    /// <summary>The sprites resolved by the last successful <c>TryResolve*</c> call; empty otherwise.</summary>
    public IReadOnlyList<SpriteBase3D> Resolved => this._resolved;

    /// <summary>True when nothing is resolved — the component has no work and should disable itself.</summary>
    public bool IsEmpty => this._resolved.Count == 0;

    /// <summary>
    /// Resolves an author's explicit list. Fails (and resolves nothing) if the list is empty or
    /// holds any entry that is not a <see cref="SpriteBase3D"/>.
    /// </summary>
    /// <param name="authored">The exported <c>Array&lt;Node&gt;</c> as the author left it.</param>
    /// <param name="error">A message naming the offending slot, ready to log verbatim; empty on success.</param>
    public bool TryResolveExplicit(GCol.Array<Node> authored, out string error)
    {
        this._resolved.Clear();

        if (authored.Count == 0)
        {
            error = "the sprite list is empty, so this component would act on nothing.";
            return false;
        }

        for (var i = 0; i < authored.Count; i++)
        {
            if (authored[i] is SpriteBase3D sprite)
            {
                this._resolved.Add(sprite);
                continue;
            }

            error = authored[i] == null
                ? $"sprite list slot {i} is empty."
                : $"sprite list slot {i} ('{authored[i].Name}') is not a SpriteBase3D.";
            this._resolved.Clear();
            return false;
        }

        error = string.Empty;
        return true;
    }

    /// <summary>
    /// Derives the set by scanning under <paramref name="root"/> (inclusive), using the shared
    /// <see cref="VisualNodeAggregator"/> scan so this stays one sprite-discovery rule rather
    /// than a second one that drifts. Fails when the scan finds nothing.
    /// </summary>
    public bool TryResolveFrom(Node root, SpriteTargetScope scope, out string error)
    {
        this._resolved.Clear();

        foreach (var node in VisualNodeAggregator.CollectSprites(root))
        {
            if (node is SpriteBase3D sprite) { this._resolved.Add(sprite); }
        }

        if (scope == SpriteTargetScope.TopMost) { this.PruneNestedSprites(root); }

        if (this._resolved.Count == 0)
        {
            error = $"no SpriteBase3D found under '{root.Name}'.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    /// <summary>
    /// The authoring-time half of <see cref="TryResolveExplicit"/>, for
    /// <c>_GetConfigurationWarnings</c>. Reports every problem rather than the first, so one
    /// pass through the scene dock fixes the whole list.
    /// </summary>
    public static string[] DescribeExplicitListProblems(GCol.Array<Node> authored)
    {
        var problems = new List<string>();

        for (var i = 0; i < authored.Count; i++)
        {
            if (authored[i] == null) { problems.Add($"Sprites[{i}] is empty."); }
            else if (authored[i] is not SpriteBase3D)
            {
                problems.Add($"Sprites[{i}] ('{authored[i].Name}') is not a SpriteBase3D.");
            }
        }

        return problems.ToArray();
    }

    /// <summary>Drops every resolved sprite, returning the set to its pre-resolution state.</summary>
    public void Clear() => this._resolved.Clear();

    /// <remarks>
    /// Walks each candidate's ancestors up to (and excluding) the scan root. Comparing against
    /// the resolved list rather than a bare <c>is SpriteBase3D</c> test keeps the rule scoped to
    /// THIS set — a sprite whose only sprite ancestor sits outside the root is still top-most
    /// here, because nothing in this set will scale that ancestor.
    /// </remarks>
    private void PruneNestedSprites(Node root)
    {
        var candidates = new HashSet<SpriteBase3D>(this._resolved);

        this._resolved.RemoveAll(sprite =>
        {
            if (sprite == root) { return false; }

            for (var parent = sprite.GetParent(); parent != null; parent = parent.GetParent())
            {
                if (parent is SpriteBase3D ancestor && candidates.Contains(ancestor)) { return true; }
                if (parent == root) { return false; }
            }

            return false;
        });
    }
}
