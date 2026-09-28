namespace Jmodot.Core.Visual.Effects;

using System;
using Godot;

/// <summary>
/// Encapsulates the runtime mechanics of a <see cref="VisualEffect"/>.
/// </summary>
/// <remarks>
/// Appliers own their Godot resources (tween and handle). <see cref="End"/> must release them.
/// The controller alone composes and writes both handle channels to tracked visuals.
/// </remarks>
public interface IEffectApplier
{
    /// <summary>
    /// Starts the effect. The applier creates and owns the tween and handle it needs
    /// using <paramref name="tree"/>.
    /// <paramref name="onFinished"/> fires when the effect completes naturally;
    /// explicit <see cref="End"/> calls must NOT trigger it.
    /// </summary>
    /// <param name="tree">Scene tree used to create Godot-side resources.</param>
    /// <param name="onFinished">Callback invoked when the effect finishes on its own.</param>
    /// <returns>
    /// The <see cref="VisualEffectHandle"/> whose <see cref="VisualEffectHandle.Modulate"/>
    /// and <see cref="VisualEffectHandle.Emission"/> the controller reads for blending.
    /// A channel the applier does not affect remains at its handle's identity value.
    /// </returns>
    VisualEffectHandle Begin(SceneTree tree, Action onFinished);

    /// <summary>
    /// Terminates the effect and frees all owned resources. Safe to call multiple times.
    /// </summary>
    void End();
}
