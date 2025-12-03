using Godot;
using Jmodot.Core.Combat;

namespace Jmodot.Implementation.Combat.Status;

public partial class DurationRevertibleStatusRunner : StatusRunner
{
    public float Duration { get; set; }
    public IRevertibleCombatEffect RevertibleEffect { get; set; }

    private Timer _durationTimer;

    public override void Start()
    {
        base.Start();

        // Apply Start Effect
        if (RevertibleEffect != null)
        {
            RevertibleEffect.Apply(Target, Context);
            // TODO: do we need to check for on effect completion?
        }

        // Setup Timer
        if (Duration > 0)
        {
            // TODO: see if scene tree timer oneshot is superior to new Timer Node.
            GetTree().CreateTimer(Duration).Timeout += Stop;
            // _durationTimer = new Timer();
            // _durationTimer.WaitTime = _duration;
            // _durationTimer.OneShot = true;
            // _durationTimer.Timeout += Stop;
            // AddChild(_durationTimer);
            // _durationTimer.Start();
        }
        else
        {
            // Instant finish if 0 duration? Or maybe infinite?
            // Assuming 0 means instant for now, or infinite if negative?
            // Let's assume > 0 is required for a duration effect.
            Stop();
        }
    }

    public override void Stop()
    {
        // Revert effect
        RevertibleEffect.TryRevert(Target, Context);
        //_durationTimer.Stop();
        base.Stop();
    }
}
