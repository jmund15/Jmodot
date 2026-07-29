namespace Jmodot.Core.Movement.Quirks;

using AI.BB;
using Shared;

/// <summary>
/// A short, arcadey locomotion deviation layered on top of steering — the channel for behavior
/// the direction-only steering pipeline structurally cannot express.
/// <para>
/// Quirks write only to the impulse channel; a quirk that wants to bias direction is a steering
/// consideration instead. They compose additively — impulses sum, with no arbitration between
/// quirks. Instances hold zero per-agent state: everything mutable lives on the
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

    public abstract void Tick(MovementQuirkRuntime runtime, in MovementQuirkContext3D ctx, float delta);
}
