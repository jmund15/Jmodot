namespace Jmodot.Core.Visual.Animation.Sprite;

/// <summary>
/// A scope that can CLAIM the base animation layer — an HSM state or a BT task that knows what the
/// body should look like while it is active. The animation authority resolves innermost-first over
/// nested scopes (BT task ⊂ HSM state ⊂ entity fallback) and pushes the winner; a scope that does
/// not claim stays silent so an outer scope wins.
/// </summary>
public interface IAnimatedState
{
    /// <summary>
    /// Whether this scope is claiming the base layer right now. The arbitration test is
    /// <c>IsAnimated == true</c> AND a non-empty <see cref="AnimationName"/>, applied identically at
    /// every tier. New implementers express it as
    /// <c>IsAnimated =&gt; !string.IsNullOrEmpty(AnimationName)</c>; a separate authored flag exists
    /// only where an author must be able to silence a scope that still carries a clip name.
    /// </summary>
    bool IsAnimated { get; }

    /// <summary>
    /// The base clip this scope claims, or null/empty when it claims nothing. Read only while the
    /// scope is the active leaf, so it may be assigned in the scope's own entry hook.
    /// </summary>
    StringName? AnimationName { get; }

    /// <summary>
    /// The claimant's OWN performance handle, for scopes that start their clip themselves for
    /// frame-exactness. The arbitration path never reads it — a claim is expressed through
    /// <see cref="IsAnimated"/> and <see cref="AnimationName"/> alone — so returning null is correct
    /// for any scope that claims without performing.
    /// </summary>
    IAnimationOrchestrator? AnimationOrchestrator { get; }
}
