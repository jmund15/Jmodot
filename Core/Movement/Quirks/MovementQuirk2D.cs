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
    /// Builds this quirk's per-agent state. <paramref name="rngOverride" /> is the determinism seam:
    /// production passes null and the subclass resolves a seeded stream off the agent's blackboard,
    /// while a pure-CLR test supplies its own <see cref="IRng" /> and never reaches the resolver.
    /// </summary>
    public abstract MovementQuirkRuntime CreateRuntime(IBlackboard? blackboard, IRng? rngOverride = null);

    public abstract void Tick(MovementQuirkRuntime runtime, in MovementQuirkContext2D ctx, float delta);
}
