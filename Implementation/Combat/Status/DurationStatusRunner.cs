using Godot;
using Jmodot.Core.Combat;

namespace Jmodot.Implementation.Combat.Status;

public partial class DurationStatusRunner : StatusRunner
{
    private readonly float _duration;
    private readonly CombatEffectFactory _onStartEffect;
    private readonly CombatEffectFactory _onEndEffect;

    private Timer _durationTimer;

    public DurationStatusRunner(float duration, CombatEffectFactory onStartEffect, CombatEffectFactory onEndEffect)
    {
        _duration = duration;
        _onStartEffect = onStartEffect;
        _onEndEffect = onEndEffect;
    }

    public override void Start()
    {
        base.Start();

        // Apply Start Effect
        if (_onStartEffect != null)
        {
            var effect = _onStartEffect.Create();
            effect.Apply(Target, Context);
        }

        // Setup Timer
        if (_duration > 0)
        {
            _durationTimer = new Timer();
            _durationTimer.WaitTime = _duration;
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
        if (_onEndEffect != null)
        {
            var effect = _onEndEffect.Create();
            effect.Apply(Target, Context);
        }

        if (_durationTimer != null) _durationTimer.Stop();
        base.Stop();
    }
}
