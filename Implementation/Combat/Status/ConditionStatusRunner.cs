using Godot;
using Jmodot.Core.Combat;

namespace Jmodot.Implementation.Combat.Status;

public partial class ConditionStatusRunner : StatusRunner
{
    public StatusCondition Condition { get; set; }
    public float CheckInterval { get; set; }
    public ICombatEffect OnTickEffect { get; set; }
    public ICombatEffect OnEndEffect { get; set; }

    private Timer _checkTimer;

    public override void Start()
    {
        base.Start();

        if (CheckInterval > 0)
        {
            _checkTimer = new Timer();
            _checkTimer.WaitTime = CheckInterval;
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
        if (CheckInterval <= 0)
        {
            OnCheck();
        }
    }

    private void OnCheck()
    {
        // Apply Tick Effect
        // Apply Tick Effect
        if (OnTickEffect != null)
        {
            OnTickEffect.Apply(Target, Context);
        }

        // Check Condition
        if (Condition != null && Condition.Check(Target, Context))
        {
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
        }

        if (_checkTimer != null) _checkTimer.Stop();
        base.Stop();
    }
}
