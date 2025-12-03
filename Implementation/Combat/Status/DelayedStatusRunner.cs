using Godot;
using Jmodot.Core.Combat;

namespace Jmodot.Implementation.Combat.Status;

public partial class DelayedStatusRunner : StatusRunner
{
    public float Delay { get; set; }
    public ICombatEffect Effect { get; set; }

    private Timer _delayTimer;

    public override void Start()
    {
        base.Start();

        if (Delay > 0)
        {
            _delayTimer = new Timer();
            _delayTimer.WaitTime = Delay;
            _delayTimer.OneShot = true;
            _delayTimer.Timeout += OnDelayFinished;
            AddChild(_delayTimer);
            _delayTimer.Start();
        }
        else
        {
            OnDelayFinished();
        }
    }

    private void OnDelayFinished()
    {
        if (Effect != null)
        {
            Effect.Apply(Target, Context);
        }
        Stop();
    }

    public override void Stop()
    {
        if (_delayTimer != null) _delayTimer.Stop();
        base.Stop();
    }
}
