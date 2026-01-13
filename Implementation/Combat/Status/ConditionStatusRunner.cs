using Godot;
using Jmodot.Core.Combat;

namespace Jmodot.Implementation.Combat.Status;

public partial class ConditionStatusRunner : StatusRunner
{
    public StatusCondition Condition { get; set; } = null!;
    public float CheckInterval { get; set; }
    public ICombatEffect OnTickEffect { get; set; } = null!;
    public ICombatEffect OnEndEffect { get; set; } = null!;

    private Timer _checkTimer = null!;

    public override void _Ready()
    {
        _checkTimer = GetNode<Timer>("CheckTimer");
        _checkTimer.OneShot = true;
        _checkTimer.Autostart = false;
    }
    public override void Start(ICombatant target, HitContext context)
    {
        base.Start(target, context);

        if (CheckInterval > 0)
        {
            _checkTimer.WaitTime = CheckInterval;
            _checkTimer.Timeout += OnCheck;
            _checkTimer.Start();
        }
        else
        {
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
        OnTickEffect.Apply(Target, Context);

        // Check Condition
        if (Condition.Check(Target, Context))
        {
            Stop();
        }
    }

    public override void Stop(bool wasDispelled = false)
    {
        // Apply End Effect
        OnEndEffect.Apply(Target, Context);
        _checkTimer.Stop();
        base.Stop(wasDispelled);
    }
}
