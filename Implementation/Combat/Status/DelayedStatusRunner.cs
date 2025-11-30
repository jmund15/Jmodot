using Godot;
using Jmodot.Core.Combat;

namespace Jmodot.Implementation.Combat.Status;

public partial class DelayedStatusRunner : StatusRunner
{
    private readonly float _delay;
    private readonly CombatEffectFactory _effectFactory;

    private Timer _delayTimer;

    public DelayedStatusRunner(float delay, CombatEffectFactory effectFactory)
    {
        _delay = delay;
        _effectFactory = effectFactory;
    }

    public override void Start()
    {
        base.Start();

        if (_delay > 0)
        {
            _delayTimer = new Timer();
            _delayTimer.WaitTime = _delay;
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
        if (_effectFactory != null)
        {
            var effect = _effectFactory.Create();
            effect.Apply(Target, Context);
        }
        Stop();
    }

    public override void Stop()
    {
        if (_delayTimer != null) _delayTimer.Stop();
        base.Stop();
    }
}
