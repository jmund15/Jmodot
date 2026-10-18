namespace Jmodot.Implementation.AI.BehaviorTree.Tasks;

using Godot;
using Movement.Quirks;

/// <summary>Shared move/pause timing for steering actions.</summary>
[GlobalClass, Tool]
public partial class MovementCadenceResource : Resource
{
    /// <summary>Seconds for which the action may steer before pausing.</summary>
    [Export(PropertyHint.Range, "0.0,5.0,0.05,suffix:s")]
    public float MoveSeconds { get; private set; }

    /// <summary>Seconds for which the action holds zero direction between move bursts.</summary>
    [Export(PropertyHint.Range, "0.0,5.0,0.05,suffix:s")]
    public float PauseSeconds { get; private set; } = 0.5f;

    /// <summary>
    /// Temporary forwarding seam: the arithmetic lives on <see cref="MovementCadenceClock"/> until
    /// this Resource is deleted with the action-owned cadence path.
    /// </summary>
    internal static bool AdvanceCadence(bool wasPaused, float cadenceTimer, float moveSeconds,
        float pauseSeconds, float delta, out bool newPaused, out float newTimer)
        => MovementCadenceClock.AdvanceCadence(
            wasPaused, cadenceTimer, moveSeconds, pauseSeconds, delta, out newPaused, out newTimer);

    #region Test Helpers
#if TOOLS
    internal void SetTimingForTest(float moveSeconds, float pauseSeconds)
    {
        MoveSeconds = moveSeconds;
        PauseSeconds = pauseSeconds;
    }
#endif
    #endregion
}
