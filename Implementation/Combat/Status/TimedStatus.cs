using Godot;
using Jmodot.Core.Combat;

namespace Jmodot.Implementation.Combat.Status;

/// <summary>
/// A status effect that expires after a set duration.
/// </summary>
public abstract class TimedStatus : ActiveStatus
{
    /// <summary>
    /// Duration in seconds.
    /// </summary>
    public float Duration { get; set; }

    /// <summary>
    /// Current time remaining in seconds.
    /// </summary>
    public float TimeRemaining { get; set; }

    public override bool IsFinished => TimeRemaining <= 0;

    public TimedStatus(float duration, Node source, ICombatant target) : base(source, target)
    {
        Duration = duration;
        TimeRemaining = duration;
    }

    public override void OnTick(double delta)
    {
        base.OnTick(delta);
        TimeRemaining -= (float)delta;
    }
}
