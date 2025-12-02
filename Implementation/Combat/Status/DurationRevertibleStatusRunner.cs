using Godot;
using Jmodot.Core.Combat;

namespace Jmodot.Implementation.Combat.Status;

public partial class DurationRevertibleStatusRunner : StatusRunner
{
    private readonly float _duration;
    private readonly IRevertibleCombatEffect _revertibleEffect;

    private Timer _durationTimer;

    public DurationRevertibleStatusRunner(float duration, IRevertibleCombatEffect revertibleEffect)
    {
        _duration = duration;
        _revertibleEffect = revertibleEffect;
    }

    public override void Start()
    {
        base.Start();

        // Apply Start Effect
        if (_revertibleEffect != null)
        {
            _revertibleEffect.Apply(Target, Context);
            // TODO: do we need to check for on effect completion?
        }

        // Setup Timer
        if (_duration > 0)
        {
            // TODO: see if scene tree timer oneshot is superior to new Timer Node.
            GetTree().CreateTimer(_duration).Timeout += Stop;
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
        _revertibleEffect.TryRevert(Target, Context);
        //_durationTimer.Stop();
        base.Stop();
    }
}
