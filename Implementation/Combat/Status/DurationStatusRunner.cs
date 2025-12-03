using Godot;
using Jmodot.Core.Combat;

namespace Jmodot.Implementation.Combat.Status;

public partial class DurationStatusRunner : StatusRunner
{
    public float Duration { get; set; }
    public ICombatEffect OnStartEffect { get; set; }
    public ICombatEffect OnEndEffect { get; set; }

    private Timer _durationTimer;

    public override void Start()
    {
        base.Start();

        // Apply Start Effect
        if (OnStartEffect != null)
        {
            OnStartEffect.Apply(Target, Context);
            // TODO: do we need to check for on effect completion?
        }

        // Setup Timer
        if (Duration > 0)
        {
            _durationTimer = new Timer();
            _durationTimer.WaitTime = Duration;
            _durationTimer.OneShot = true;
            _durationTimer.Timeout += Stop;
            AddChild(_durationTimer);
            _durationTimer.Start();
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
        // Apply End Effect
        // Apply End Effect
        if (OnEndEffect != null)
        {
            OnEndEffect.Apply(Target, Context);
            // TODO: do we need to check for on effect completion?
        }

        if (_durationTimer != null) _durationTimer.Stop();
        base.Stop();
    }
}
