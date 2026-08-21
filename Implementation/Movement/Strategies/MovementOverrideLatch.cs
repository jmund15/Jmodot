namespace Jmodot.Implementation.Movement.Strategies;

using Core.Actors;
using Core.Movement.Strategies;

/// <summary>
/// One action's hold on a movement processor's single strategy-override slot, tracked per instance so
/// several actions can share the discipline without sharing the state.
/// </summary>
/// <remarks>
/// <para>
/// Two invariants are centralized here rather than re-derived per phase. First, the latch only ever
/// clears the override it took itself: an action's <c>OnExit</c> runs on every path including
/// preemption, and clearing unconditionally would stomp the knockback or freeze state that did the
/// preempting. Second, <see cref="Apply"/> clears before it sets, which is what makes a
/// windup-halt → ballistic swap safe to perform immediately before an impulse — a zero-velocity
/// strategy still holding the slot replaces the impulse-modified velocity and the launch travels
/// nowhere.
/// </para>
/// <para>
/// A struct, and a field on the owning action rather than a static on
/// <c>AttackActionHelpers</c>: the flag is per-action mutable state, so a static forwarder
/// would be a shared mutable slot dressed as a helper. Declare it as a plain field
/// (<c>private MovementOverrideLatch _latch;</c>) — mutating through a property or a <c>readonly</c>
/// field would mutate a copy.
/// </para>
/// </remarks>
public struct MovementOverrideLatch
{
    private IMovementProcessor3D? _movement;

    /// <summary>True while this latch holds the processor's override slot.</summary>
    public bool IsActive { get; private set; }

    /// <summary>
    /// Hand the slot back if held, then take it for <paramref name="strategy"/>. A null strategy (or a
    /// null <paramref name="movement"/>) leaves the processor's default locomotion in charge.
    /// </summary>
    public void Apply(IMovementProcessor3D? movement, BaseMovementStrategy3D? strategy)
    {
        this.Restore();

        this._movement = movement;
        if (movement == null || strategy == null) { return; }

        movement.SetStrategyOverride((IMovementStrategy3D)strategy);
        this.IsActive = true;
    }

    /// <summary>
    /// Release the slot. Idempotent, and inert when this latch never took it — safe to call from an
    /// <c>OnExit</c> that runs whether the action ever reached the phase that applied an override.
    /// </summary>
    public void Restore()
    {
        if (!this.IsActive) { return; }

        this.IsActive = false;
        this._movement?.ClearStrategyOverride();
    }
}
