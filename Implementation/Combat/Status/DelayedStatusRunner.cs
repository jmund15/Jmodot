using Godot;
using Jmodot.Core.Combat;
using Jmodot.Core.Visual.Effects;

namespace Jmodot.Implementation.Combat.Status;

using System.Collections.Generic;

public partial class DelayedStatusRunner : StatusRunner
{
    public float Delay { get; set; }
    public ICombatEffect Effect { get; set; }

    private Timer _delayTimer;

    public void Setup(float delay,
        ICombatEffect effect,
        PackedScene? persistantVisuals, IEnumerable<CombatTag> tags,
        VisualEffect? visualEffect = null)
    {
        Delay = delay;
        Effect = effect;
        PersistentVisuals = persistantVisuals;
        Tags = tags;
        StatusVisualEffect = visualEffect;
    }

    public override void _Ready()
    {
        _delayTimer = GetNodeOrNull<Timer>("DelayTimer");
        if (_delayTimer == null)
        {
            _delayTimer = new Timer { Name = "DelayTimer" };
            AddChild(_delayTimer);
        }

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
        _delayTimer.Timeout -= OnDelayFinished;
        _delayTimer.Stop();
        base.Stop(wasDispelled);
    }
}
