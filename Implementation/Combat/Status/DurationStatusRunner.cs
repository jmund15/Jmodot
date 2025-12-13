using Godot;
using Jmodot.Core.Combat;

namespace Jmodot.Implementation.Combat.Status;

using System.Collections.Generic;

public partial class DurationStatusRunner : StatusRunner
{
    public float Duration { get; set; }
    public ICombatEffect OnStartEffect { get; set; }
    public ICombatEffect OnEndEffect { get; set; }

    private Timer _durationTimer;

    public void Setup(float duration,
        ICombatEffect startEffect, ICombatEffect endEffect,
        PackedScene? persistantVisuals, IEnumerable<GameplayTag> tags)
    {
        Duration = duration;
        OnStartEffect = startEffect;
        OnEndEffect = endEffect;
        PersistentVisuals = persistantVisuals;
        Tags = tags;
    }

    public override void _Ready()
    {
        _durationTimer = GetNode<Timer>("DurationTimer");
        _durationTimer.OneShot = true;
        _durationTimer.Autostart = false;
    }

    public override void Start(ICombatant target, HitContext context)
    {
        base.Start(target, context);

        Target.ApplyEffect(OnStartEffect, Context);

        // Setup Timer
        if (Duration > 0)
        {
            _durationTimer.WaitTime = Duration;
            _durationTimer.Timeout += () => Stop();
            _durationTimer.Start();
        }
        else
        {
            // > 0 is required for a duration effect.
            Stop();
        }
    }

    public override void Stop(bool wasDispelled = false)
    {
        // Apply End Effect
        Target.ApplyEffect(OnEndEffect, Context);
        _durationTimer.Stop();

        base.Stop(wasDispelled);
    }
}
