using System;
using Godot;
using Jmodot.Core.Combat;
using Jmodot.Core.Combat.Status;
using Jmodot.Core.Visual.Effects;

namespace Jmodot.Implementation.Combat.Status;

using System.Collections.Generic;

public partial class TickStatusRunner : StatusRunner, IDurationModifiable, IDurationRefreshable, IAmplifiable
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
            var visual = TickVisuals.Instantiate() as Node3D;
            // TODO: add config for if visuals should be parented to the target or the status effect component
            Target.OwnerNode.AddChild(visual);
            //AddChild(visual);
            //visual.GlobalPosition = (Target.OwnerNode as Node3D).GlobalPosition;
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

    #region IDurationModifiable Implementation

    /// <inheritdoc />
    public float RemainingDuration => (float)(_durationTimer?.TimeLeft ?? 0.0);

    /// <inheritdoc />
    public void ReduceDuration(float amount)
    {
        if (_durationTimer == null || _durationTimer.IsStopped())
        {
            return;
        }

        var newDuration = Math.Max(0f, (float)_durationTimer.TimeLeft - amount);
        SetDuration(newDuration);
    }

    /// <inheritdoc />
    public void ExtendDuration(float amount)
    {
        if (_durationTimer == null)
        {
            return;
        }

        var newDuration = (float)_durationTimer.TimeLeft + amount;
        SetDuration(newDuration);
    }

    /// <inheritdoc />
    public void SetDuration(float newDuration)
    {
        if (_durationTimer == null)
        {
            return;
        }

        _durationTimer.Stop();

        if (newDuration <= 0)
        {
            Stop();
            return;
        }

        _durationTimer.WaitTime = newDuration;
        _durationTimer.Start();
    }

    #endregion

    #region IDurationRefreshable Implementation

    /// <inheritdoc />
    public void RefreshDuration(StatusRunner source)
    {
        if (source is TickStatusRunner tickSource)
        {
            SetDuration(tickSource.Duration);
        }
        else if (source is DurationStatusRunner durationSource)
        {
            SetDuration(durationSource.Duration);
        }
    }

    #endregion

    #region IAmplifiable Implementation

    /// <inheritdoc />
    public void Amplify(float magnitude)
    {
        // For tick runners, amplify means extend duration (more ticks)
        ExtendDuration(magnitude);
    }

    #endregion
}
