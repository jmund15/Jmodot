using Godot;
using Jmodot.Core.Combat;

namespace Jmodot.Implementation.Combat.Status;

public partial class TickStatusRunner : StatusRunner
{
    private readonly float _duration;
    private readonly float _interval;
    private readonly CombatEffectFactory _effectFactory;

    private Timer _tickTimer;
    private Timer _durationTimer;

    public TickStatusRunner(float duration, float interval, CombatEffectFactory effectFactory)
    {
        _duration = duration;
        _interval = interval;
        _effectFactory = effectFactory;
    }

    public override void Start()
    {
        base.Start();

        // Setup Duration Timer
        if (_duration > 0)
        {
            _durationTimer = new Timer();
            _durationTimer.WaitTime = _duration;
            _durationTimer.OneShot = true;
            _durationTimer.Timeout += Stop;
            AddChild(_durationTimer);
            _durationTimer.Start();
        }

        // Setup Tick Timer
        if (_interval > 0 && _effectFactory != null)
        {
            _tickTimer = new Timer();
            _tickTimer.WaitTime = _interval;
            _tickTimer.OneShot = false;
            _tickTimer.Timeout += OnTick;
            AddChild(_tickTimer);
            _tickTimer.Start();
        }
    }

    private void OnTick()
    {
        if (_effectFactory != null)
        {
            var effect = _effectFactory.Create();
            effect.Apply(Target, Context);
        }
    }

    public override void Stop()
    {
        if (_tickTimer != null) _tickTimer.Stop();
        if (_durationTimer != null) _durationTimer.Stop();
        base.Stop();
    }
}
