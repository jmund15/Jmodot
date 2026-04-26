namespace Jmodot.Core.Visual.Animation.Sprite;

using System;

/// <summary>
/// Behavior flags for <c>VisualSlotNode.Push</c>. Apply only to the duration of the push;
/// the prior item's options are restored on <c>Pop</c>.
/// </summary>
[Flags]
public enum PushOptions
{
    None = 0,

    /// <summary>
    /// While this push is active, skip composite-animator registration for the slot's
    /// equipped animator. Use for transient overlay animations (e.g. potion-add hand)
    /// that should NOT be driven by the body composite's master timing.
    /// </summary>
    AsAnimationIndependent = 1 << 0,
}
