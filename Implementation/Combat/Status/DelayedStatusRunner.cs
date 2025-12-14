using Godot;
using Jmodot.Core.Combat;

namespace Jmodot.Implementation.Combat.Status;

using System.Collections.Generic;

public partial class DelayedStatusRunner : StatusRunner
{
    public float Delay { get; set; }
    public ICombatEffect Effect { get; set; }

    private Timer _delayTimer;

    public void Setup(float delay,
        ICombatEffect effect,
        PackedScene? persistantVisuals, IEnumerable<CombatTag> tags)
    {
        Delay = delay;
        Effect = effect;
        PersistentVisuals = persistantVisuals;
        Tags = tags;
    }

    public override void _Ready()
    {
        _delayTimer = GetNode<Timer>("DelayTimer");
        _delayTimer.OneShot = true;
        _delayTimer.Autostart = false;
    }

    public override void Start(ICombatant target, HitContext context)
    {
        base.Start(target, context);

        if (Delay > 0)
        {
            _delayTimer.WaitTime = Delay;
            _delayTimer.Timeout += OnDelayFinished;
            _delayTimer.Start();
        }
        else
        {
            OnDelayFinished();
        }
    }

    private void OnDelayFinished()
    {
        Target.ApplyEffect(Effect, Context);
        Stop();
    }

    public override void Stop(bool wasDispelled = false)
    {
        _delayTimer.Stop();
        base.Stop(wasDispelled);
    }
}
