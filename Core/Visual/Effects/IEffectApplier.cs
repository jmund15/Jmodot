namespace Jmodot.Core.Visual.Effects;

using System;
using Godot;

/// <summary>
/// Encapsulates the runtime mechanics of a <see cref="VisualEffect"/>.
/// </summary>
/// <remarks>
/// <para>
/// Today's effects (<c>FlashEffect</c>, <c>TintEffect</c>) are Modulate-tween based
/// and share the implementation <c>ModulateTweenApplier</c>. Future effect kinds
/// (glow-shader, particle burst, per-node gradient) can ship their own applier
/// without <c>VisualEffectController</c> ever seeing the details — the controller
/// treats appliers as opaque "start / stop" units with a shared modulate handle for
/// the Modulate-blending case.
/// </para>
/// <para>
/// Appliers own their Godot resources (tween, shader material, emitters). End()
/// MUST release them. The controller calls End() on every remove / stop path.
/// </para>
/// </remarks>
public interface IEffectApplier
{
    /// <summary>
    /// Starts the effect. The applier creates and owns any Godot objects it needs
    /// (tween, shader material, etc.) parented under <paramref name="tree"/>.
    /// <paramref name="onFinished"/> fires when the effect completes naturally;
    /// explicit <see cref="End"/> calls must NOT trigger it.
    /// </summary>
    /// <param name="tree">Scene tree used to create Godot-side resources.</param>
    /// <param name="onFinished">Callback invoked when the effect finishes on its own.</param>
    /// <returns>
    /// The <see cref="VisualEffectHandle"/> whose <c>Modulate</c> the controller
    /// reads for blending. Appliers that do not participate in Modulate blending
    /// may return a dummy handle that stays at <see cref="Colors.White"/>.
    /// </returns>
    VisualEffectHandle Begin(SceneTree tree, Action onFinished);

    /// <summary>
    /// Terminates the effect and frees all owned resources. Safe to call multiple times.
    /// </summary>
    void End();
}
