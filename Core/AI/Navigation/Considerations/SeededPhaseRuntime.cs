namespace Jmodot.Core.AI.Navigation.Considerations;

/// <summary>
/// Per-agent runtime for any consideration whose scoring is driven by a desynchronized time
/// accumulator: a fixed <see cref="Offset"/> folded from the agent's entity seed (so agents
/// sharing one .tres resource don't move in lockstep) plus this agent's own
/// <see cref="AccumulatedTime"/>, advanced once per evaluation by the caller.
/// </summary>
public class SeededPhaseRuntime : AIConsiderationRuntime
{
    public float Offset;
    public float AccumulatedTime;
}
