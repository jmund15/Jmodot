namespace Jmodot.Core.AI.Navigation;

using Godot;
using Implementation.AI.Navigation;

/// <summary>
/// Pure, stateless strategy that collapses a <see cref="SteeringContextMap"/> into a single desired
/// direction. Per-agent commitment state is NOT held here — it flows through <see cref="Synthesize"/>
/// as input and output, so one shared <c>.tres</c> instance is safe across every agent. Follows the
/// <c>TurnRateProfile3D</c> pure-Resource-strategy precedent.
/// </summary>
[GlobalClass, Tool]
public abstract partial class SteeringSynthesisStrategy3D : Resource
{
    /// <summary>Interest-vs-danger trade-off shared by every strategy (feeds
    /// <see cref="SteeringContextMap.EffectiveScore"/>). One knob on the base — no per-subclass duplication.</summary>
    [Export(PropertyHint.Range, "0.0, 3.0, 0.05")] protected float DangerScale { get; private set; } = 1.0f;

    /// <summary>Pure: previous state in (readonly), new state out. Implementations hold zero mutable
    /// fields beyond their exports.</summary>
    public abstract (Vector3 Direction, SteeringSynthesisState NewState) Synthesize(
        SteeringContextMap map, in SteeringSynthesisState state);

    /// <summary>Bin with the lowest <see cref="SteeringContextMap.Danger"/> (ties resolve to the lowest
    /// index). The shared fallback when every bin is hard-masked. Assumes at least one bin.</summary>
    protected static int LeastDangerBin(SteeringContextMap map)
    {
        int least = 0;
        for (int i = 1; i < map.Bins.Count; i++)
        {
            if (map.Danger[i] < map.Danger[least]) { least = i; }
        }
        return least;
    }

    #region Test Helpers
#if TOOLS
    internal void SetDangerScale(float value) => DangerScale = value;
#endif
    #endregion
}

/// <summary>Per-agent synthesis memory. Lives on the processor Node (pool-safe); threaded through
/// <see cref="SteeringSynthesisStrategy3D.Synthesize"/> so the strategy Resource stays stateless.</summary>
public struct SteeringSynthesisState
{
    public int CommittedBin;      // -1 = none committed
    public Vector3 LastDirection; // Vector3.Zero = none
    public static SteeringSynthesisState Empty => new() { CommittedBin = -1 };
}
