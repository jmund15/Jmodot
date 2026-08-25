namespace Jmodot.Implementation.AI.BehaviorTree.Tasks;

using Godot;

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

    /// <summary>Advances one move/pause cadence tick and reports whether steering may run.</summary>
    internal static bool AdvanceCadence(bool wasPaused, float cadenceTimer, float moveSeconds,
        float pauseSeconds, float delta, out bool newPaused, out float newTimer)
    {
        var remaining = cadenceTimer - delta;
        if (remaining > 0f)
        {
            newPaused = wasPaused;
            newTimer = remaining;
            return !wasPaused;
        }

        newPaused = !wasPaused;
        if (!newPaused || !(pauseSeconds > 0f))
        {
            newPaused = false;
            newTimer = moveSeconds;
            return true;
        }

        newTimer = pauseSeconds;
        return false;
    }
}
