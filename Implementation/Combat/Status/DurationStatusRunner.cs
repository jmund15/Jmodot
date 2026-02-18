using System;
using Godot;
using Jmodot.Core.Combat;
using Jmodot.Core.Combat.Status;
using Jmodot.Core.Visual.Effects;

namespace Jmodot.Implementation.Combat.Status;

using System.Collections.Generic;

public partial class DurationStatusRunner : StatusRunner, IDurationModifiable, IDurationRefreshable, IAmplifiable
{
    public float Duration { get; set; }
    public ICombatEffect? OnStartEffect { get; set; }
    public ICombatEffect? OnEndEffect { get; set; }

    private Timer _durationTimer;

    public void Setup(float duration,
        ICombatEffect? startEffect, ICombatEffect? endEffect,
        PackedScene? persistantVisuals, IEnumerable<CombatTag> tags,
        VisualEffect? visualEffect = null)
    {
        Duration = duration;
        OnStartEffect = startEffect;
        OnEndEffect = endEffect;
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

        if (OnStartEffect != null)
        {
            Target.ApplyEffect(OnStartEffect, Context);
        }

        // Setup Timer
        if (Duration > 0)
        {
            _durationTimer.WaitTime = Duration;
            _durationTimer.Timeout += OnDurationExpired;
            _durationTimer.Start();
        }
        else
        {
            // > 0 is required for a duration effect.
            Stop();
        }
    }

    private void OnDurationExpired() => Stop();

    public override void Stop(bool wasDispelled = false)
    {
        // Apply End Effect
        if (OnEndEffect != null)
        {
            Target.ApplyEffect(OnEndEffect, Context);
        }
        _durationTimer.Timeout -= OnDurationExpired;
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
        if (source is DurationStatusRunner durationSource)
        {
            SetDuration(durationSource.Duration);
        }
    }

    #endregion

    #region IAmplifiable Implementation

    /// <inheritdoc />
    public void Amplify(float magnitude)
    {
        // For duration runners, amplify means extend duration
        ExtendDuration(magnitude);
    }

    #endregion
}
