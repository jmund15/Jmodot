namespace Jmodot.Core.Movement.Quirks;

using AI.BB;
using Shared;

/// <summary>
/// A short, arcadey locomotion deviation layered on top of steering — the channel for behavior
/// the direction-only steering pipeline structurally cannot express.
/// <para>
/// Quirks have two channels. The drive gate answers whether voluntary movement is allowed this
/// tick; all active gates are answered before any impulse tick, and every gate must permit for
/// the agent to drive. The impulse channel writes forces and composes additively — impulses sum,
/// with no arbitration between quirks. A quirk that wants to bias direction is a steering
/// consideration instead. Instances hold zero per-agent state: everything mutable lives on the
/// <see cref="MovementQuirkRuntime" /> the processor owns.
/// </para>
/// Dimension-parallel sibling: <see cref="MovementQuirk2D" />.
/// </summary>
[GlobalClass, Tool]
public abstract partial class MovementQuirk3D : Resource
{
    /// <summary>
    /// Builds this quirk's per-agent state. <paramref name="rng" /> is always a live stream — the
    /// per-agent processor resolves it once and hands it down, so no subclass resolves or latches
    /// anything itself. That is what keeps the shared Resource free of per-agent mutable state.
    /// </summary>
    public abstract MovementQuirkRuntime CreateRuntime(IBlackboard? blackboard, IRng rng);

    /// <summary>
    /// Reports whether voluntary drive is allowed this tick. Called exactly once per active quirk
    /// per tick, before any <see cref="Tick" /> call; the product of every gate decides. The default
    /// permits drive, so an impulse-only quirk overrides <see cref="Tick" /> alone and a gate-only
    /// quirk overrides this method alone. A quirk with an invalid configuration must return true:
    /// bad data may remove the quirk's rhythm, never freeze the agent.
    /// <para>
    /// While the processor holds a drive suspension (a step's scoped gait opt-out) this is not
    /// called and the clock behind it does not advance.
    /// </para>
    /// </summary>
    public virtual bool TickDrive(MovementQuirkRuntime runtime, float delta) => true;

    /// <summary>
    /// Writes this tick's impulses. Called once per active quirk per tick, after every drive gate
    /// has answered. The default does nothing, so a gate-only quirk needs no empty override.
    /// </summary>
    public virtual void Tick(MovementQuirkRuntime runtime, in MovementQuirkContext3D ctx, float delta) { }
}
