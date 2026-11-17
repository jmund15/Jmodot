namespace Jmodot.Implementation.Movement.Quirks;

using System.Collections.Generic;
using Core.AI.BB;
using Core.Movement.Quirks;
using Core.Shared;
using Shared;

/// <summary>
/// A move/pause rhythm for voluntary drive: the agent moves for <see cref="MoveSeconds"/>, then
/// pauses for <see cref="PauseSeconds"/>, cycling. It gates the drive channel only — steering,
/// navigation, impulses and external forces keep running through the pause.
/// <para>
/// The start phase comes from the entity seed's <see cref="SeedKinds.CadencePhase"/> unit, so two
/// agents sharing one gait do not step in lockstep, and creating a runtime draws nothing from the
/// quirk RNG stream. The Resource holds configuration only; the clock lives on the runtime.
/// </para>
/// <para>
/// Every active gait must permit drive. A step that must not stall (an escape or a pursuit) opts
/// out with <c>BehaviorAction.SuspendGait</c> instead of authoring a second rhythm.
/// </para>
/// </summary>
[GlobalClass, Tool]
public partial class CadenceQuirk3D : MovementQuirk3D, IResourceConfigurationWarnings
{
    /// <summary>
    /// Seconds of voluntary movement before the pause. All active gaits must permit movement.
    /// </summary>
    [Export(PropertyHint.Range, "0.05,10,0.05,suffix:s")]
    public float MoveSeconds { get; private set; } = 0.4f;

    /// <summary>
    /// Seconds of pause after movement. Steering still evaluates; impulses and external forces
    /// still act.
    /// </summary>
    [Export(PropertyHint.Range, "0.05,10,0.05,suffix:s")]
    public float PauseSeconds { get; private set; } = 0.5f;

    private bool _warnedNoSeed;

    /// <summary>
    /// Starts this agent's clock at the entity-seeded phase. Consumes no quirk-RNG draw: the phase
    /// is the per-kind unit derived from <c>BBDataSig.EntitySeed</c>.
    /// </summary>
    public override MovementQuirkRuntime CreateRuntime(IBlackboard? blackboard, IRng rng)
    {
        float unit = EntityRngResolver.ResolveUnit(
            blackboard, SeedKinds.CadencePhase, this, ref _warnedNoSeed);

        var runtime = new CadenceRuntime(rng);
        runtime.Clock.Start(MoveSeconds, PauseSeconds, unit);
        return runtime;
    }

    /// <summary>
    /// Advances the clock and reports whether voluntary drive is permitted. Non-positive or
    /// non-finite periods always permit: bad data may remove the rhythm, never freeze the agent.
    /// </summary>
    public override bool TickDrive(MovementQuirkRuntime runtime, float delta)
    {
        if (runtime is not CadenceRuntime cadence) { return true; }
        return cadence.Clock.Advance(MoveSeconds, PauseSeconds, delta);
    }

    /// <summary>
    /// One entry per invalid period, naming the Inspector label the author would look for. A valid
    /// default-constructed gait yields none.
    /// </summary>
    public string[] GetResourceConfigurationWarnings()
    {
        var warnings = new List<string>(2);
        if (!IsUsablePeriod(MoveSeconds))
        {
            warnings.Add("Move Seconds must be a finite, positive number of seconds; drive is left unrestricted.");
        }
        if (!IsUsablePeriod(PauseSeconds))
        {
            warnings.Add("Pause Seconds must be a finite, positive number of seconds; drive is left unrestricted.");
        }
        return warnings.ToArray();
    }

    private static bool IsUsablePeriod(float seconds) => float.IsFinite(seconds) && seconds > 0f;

    internal sealed class CadenceRuntime : MovementQuirkRuntime
    {
        public CadenceRuntime(IRng rng) : base(rng) { }

        public MovementCadenceClock Clock;
    }

    #region Test Helpers
#if TOOLS
    internal void SetPeriodsForTest(float moveSeconds, float pauseSeconds)
    {
        MoveSeconds = moveSeconds;
        PauseSeconds = pauseSeconds;
    }
#endif
    #endregion
}
