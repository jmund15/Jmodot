namespace Jmodot.Core.Movement.Quirks;

using AI.BB;
using Shared;

/// <summary>
/// 2D mirror of <see cref="MovementQuirk3D" /> — see that type for the impulse-channel and
/// zero-per-agent-state contracts.
/// </summary>
[GlobalClass, Tool]
public abstract partial class MovementQuirk2D : Resource
{
    /// <summary>
    /// Builds this quirk's per-agent state. <paramref name="rng" /> is always a live stream — the
    /// per-agent processor resolves it once and hands it down, so no subclass resolves or latches
    /// anything itself. That is what keeps the shared Resource free of per-agent mutable state.
    /// </summary>
    public abstract MovementQuirkRuntime CreateRuntime(IBlackboard? blackboard, IRng rng);

    public abstract void Tick(MovementQuirkRuntime runtime, in MovementQuirkContext2D ctx, float delta);
}
