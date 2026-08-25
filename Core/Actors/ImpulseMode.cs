namespace Jmodot.Core.Actors;

/// <summary>
/// How an impulse composes with the velocity the movement pump produces for the same frame.
/// </summary>
public enum ImpulseMode
{
    /// <summary>
    /// The impulse is added on top of the strategy's velocity — the default, and what push,
    /// knockback and force zones want.
    /// </summary>
    Add,

    /// <summary>
    /// The impulse BECOMES the frame's velocity, discarding both the strategy's output and any
    /// residual the body carried in. For a launch or rebound whose direction was solved
    /// analytically: composing it with whatever the body happened to be doing aims it somewhere
    /// neither the solution nor the prior heading names.
    /// </summary>
    /// <remarks>
    /// Reconstructing this as <c>SetVelocity(Zero)</c> before <c>ApplyImpulse</c> at the call site
    /// does NOT work: the pump runs the strategy's <c>SetVelocity</c> afterwards, so the zeroing
    /// survives only when an active still-strategy happens to hold the body at rest. Ask for
    /// Replace instead of arranging it.
    /// </remarks>
    Replace,
}
