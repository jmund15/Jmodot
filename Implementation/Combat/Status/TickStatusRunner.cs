using Godot;
using Jmodot.Core.Combat;
using Jmodot.Core.Visual.Effects;

namespace Jmodot.Implementation.Combat.Status;

using System.Collections.Generic;

public partial class TickStatusRunner : StatusRunner
{
    public float Duration { get; private set; }
    public float Interval { get; private set; }
    public ICombatEffect TickEffect { get; private set; }

    /// <summary>
    /// Optional visual scene to spawn on each tick.
    /// </summary>
    public PackedScene? TickVisuals { get; set; }

    private Timer _tickTimer;
    private Timer _durationTimer;

    public void Setup(float duration, float interval, ICombatEffect tickEffect, PackedScene? tickVisuals,
        PackedScene? persistantVisuals, IEnumerable<CombatTag> tags,
        VisualEffect? visualEffect = null)
    {
        Duration = duration;
        Interval = interval;
        TickEffect = tickEffect;
        TickVisuals = tickVisuals;
        PersistentVisuals = persistantVisuals;
        Tags = tags;
        StatusVisualEffect = visualEffect;
    }
    public override void _Ready()
    {
        _tickTimer = GetNodeOrNull<Timer>("TickTimer");
        if (_tickTimer == null)
        {
            _tickTimer = new Timer { Name = "TickTimer" };
            AddChild(_tickTimer);
        }

        _durationTimer = GetNodeOrNull<Timer>("DurationTimer");
        if (_durationTimer == null)
        {
            _durationTimer = new Timer { Name = "DurationTimer" };
            AddChild(_durationTimer);
        }

        _tickTimer.OneShot = false;
        _tickTimer.Autostart = false;
        _durationTimer.OneShot = true;
        _durationTimer.Autostart = false;
    }

    public override void Start(ICombatant target, HitContext context)
    {
        base.Start(target, context);

        // Setup Duration Timer
        if (Duration > 0)
        {
            _durationTimer.WaitTime = Duration;
            _durationTimer.Timeout += () => Stop();
            _durationTimer.Start();
        }

        // Setup Tick Timer
        if (Interval > 0)
        {
            _tickTimer.WaitTime = Interval;
            _tickTimer.Timeout += OnTick;
            _tickTimer.Start();
        }
    }

    private void OnTick()
    {
        // Spawn Visuals
        if (TickVisuals != null)
        {
            var visual = TickVisuals.Instantiate();
            AddChild(visual);
        }

        if (TickEffect != null)
        {
            TickEffect.Apply(Target, Context);
        }
    }

    public override void Stop(bool wasDispelled = false)
    {
        _tickTimer.Stop();
        _durationTimer.Stop();
        base.Stop(wasDispelled);
    }
}
