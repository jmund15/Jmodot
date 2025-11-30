using Godot;
using Jmodot.Core.Combat;

namespace Jmodot.Implementation.Combat.Status;

public partial class ConditionStatusRunner : StatusRunner
{
    private readonly StatusCondition _condition;
    private readonly float _checkInterval;
    private readonly CombatEffectFactory _onTickEffect;
    private readonly CombatEffectFactory _onEndEffect;

    private Timer _checkTimer;

    public ConditionStatusRunner(StatusCondition condition, float checkInterval, CombatEffectFactory onTickEffect, CombatEffectFactory onEndEffect)
    {
        _condition = condition;
        _checkInterval = checkInterval;
        _onTickEffect = onTickEffect;
        _onEndEffect = onEndEffect;
    }

    public override void Start()
    {
        base.Start();

        if (_checkInterval > 0)
        {
            _checkTimer = new Timer();
            _checkTimer.WaitTime = _checkInterval;
            _checkTimer.OneShot = false;
            _checkTimer.Timeout += OnCheck;
            AddChild(_checkTimer);
            _checkTimer.Start();
        }
        else
        {
            // If 0, check every frame? For now, let's enforce a minimum interval or use Process.
            // Using Process for 0 interval.
            SetProcess(true);
        }
    }

    public override void _Process(double delta)
    {
        if (_checkInterval <= 0)
        {
            OnCheck();
        }
    }

    private void OnCheck()
    {
        // Apply Tick Effect
        if (_onTickEffect != null)
        {
            var effect = _onTickEffect.Create();
            effect.Apply(Target, Context);
        }

        // Check Condition
        if (_condition != null && _condition.Check(Target, Context))
        {
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

        if (_checkTimer != null) _checkTimer.Stop();
        base.Stop();
    }
}
