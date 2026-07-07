namespace Jmodot.Implementation.Visual.Effects;

using Godot;
using Jmodot.Implementation.Shared;

/// <summary>
/// Composable fade tweening for an entity's 3D visual families, each independently
/// parameterized: sprites (SpriteBase3D — the shared base of Sprite3D and
/// AnimatedSprite3D — via modulate alpha) and meshes (MeshInstance3D via transparency).
///
/// The Add* methods append tweeners for one family to a caller-owned Tween and return
/// the count added — compose them for custom fades (sprite-only, staggered families,
/// caller-managed tween lifecycle). <see cref="StartFadeOut"/> is the batteries-included
/// composite for the common "fade everything, then act on Finished" shape.
/// </summary>
public static class VisualFader3D
{
    /// <summary>
    /// Appends a modulate-alpha tweener for every SpriteBase3D under root.
    /// Returns the number of tweeners added.
    /// </summary>
    public static int AddSpriteFadeTweeners(Tween tween, Node root, float duration, SpriteFadeParams? spriteParams = null)
    {
        var p = spriteParams ?? SpriteFadeParams.Default;
        int added = 0;
        foreach (var sprite in root.GetChildrenOfType<SpriteBase3D>())
        {
            tween.TweenProperty(sprite, "modulate:a", p.TargetAlpha, duration)
                .SetEase(p.Ease)
                .SetTrans(p.Transition);
            added++;
        }
        return added;
    }

    /// <summary>
    /// Appends a transparency tweener for every MeshInstance3D under root.
    /// Returns the number of tweeners added.
    /// </summary>
    public static int AddMeshFadeTweeners(Tween tween, Node root, float duration, MeshFadeParams? meshParams = null)
    {
        var p = meshParams ?? MeshFadeParams.Default;
        int added = 0;
        foreach (var mesh in root.GetChildrenOfType<MeshInstance3D>())
        {
            tween.TweenProperty(mesh, "transparency", p.TargetTransparency, duration)
                .SetEase(p.Ease)
                .SetTrans(p.Transition);
            added++;
        }
        return added;
    }

    /// <summary>
    /// Starts a parallel fade of both visual families under <paramref name="root"/>.
    /// Returns null when the root is invalid/out-of-tree or nothing under it is fadeable —
    /// a Tween with zero tweeners errors and never fires Finished, so callers that hang
    /// teardown (QueueFree, completion callbacks) off Finished MUST take that path
    /// synchronously on a null return.
    /// </summary>
    public static Tween? StartFadeOut(
        Node3D root,
        float duration,
        SpriteFadeParams? spriteParams = null,
        MeshFadeParams? meshParams = null)
    {
        if (!GodotObject.IsInstanceValid(root) || !root.IsInsideTree()) { return null; }

        var tween = root.GetTree().CreateTween();
        tween.SetParallel(true);

        int tweeners = AddSpriteFadeTweeners(tween, root, duration, spriteParams)
            + AddMeshFadeTweeners(tween, root, duration, meshParams);

        if (tweeners == 0)
        {
            tween.Kill();
            return null;
        }

        tween.Play();
        return tween;
    }
}
