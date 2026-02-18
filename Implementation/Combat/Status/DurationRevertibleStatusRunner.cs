using Godot;
using Jmodot.Core.Combat;
using Jmodot.Core.Visual.Effects;

namespace Jmodot.Implementation.Combat.Status;

using System.Collections.Generic;

public partial class DurationRevertibleStatusRunner : StatusRunner
{
    public float Duration { get; private set; }
    public IRevertibleCombatEffect RevertibleEffect { get; private set; } = null!;

    private Timer _durationTimer = null!;

    public void Setup(float duration, IRevertibleCombatEffect revertibleEffect,
        PackedScene? persistantVisuals, IEnumerable<CombatTag> tags,
        VisualEffect? visualEffect = null)
    {
        Duration = duration;
        RevertibleEffect = revertibleEffect;
        PersistentVisuals = persistantVisuals;
        Tags = tags;
        StatusVisualEffect = visualEffect;
    }
    public override void _Ready()
    {
        _durationTimer = GetNodeOrNull<Timer>("DurationTimer");
        if (_durationTimer == null)
        {
            _durationTimer = new Timer { Name = "DurationTimer" };
            AddChild(_durationTimer);
        }

        _durationTimer.OneShot = true;
        _durationTimer.Autostart = false;
    }

    public override void Start(ICombatant target, HitContext context)
    {
        base.Start(target, context);

        // Apply Start Effect
        Target.ApplyEffect(RevertibleEffect!, Context);

        // Setup Timer
        if (Duration > 0)
        {
            // TODO: see if scene tree timer oneshot is superior to new Timer Node.
            //GetTree().CreateTimer(Duration).Timeout += () => Stop();

            _durationTimer.WaitTime = Duration;
            _durationTimer.Timeout += OnDurationExpired;
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

    private void OnDurationExpired() => Stop();

    public override void Stop(bool wasDispelled = false)
    {
        // Revert effect (null-guard: GetRevertEffect returns null when stat wasn't applied)
        var revertEffect = RevertibleEffect.GetRevertEffect(Target, Context);
        if (revertEffect != null)
        {
            Target.ApplyEffect(revertEffect, Context);
        }
        _durationTimer.Timeout -= OnDurationExpired;
        _durationTimer.Stop();

        base.Stop(wasDispelled);
    }
}
