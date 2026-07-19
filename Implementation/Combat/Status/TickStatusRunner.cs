using System;
using Godot;
using Jmodot.Core.Combat;
using Jmodot.Core.Combat.Status;
using Jmodot.Core.Visual.Effects;
using Jmodot.Implementation.Shared;

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

    /// <summary>
    /// Optional per-tick visual effect (flash/pulse) played via VisualEffectController on each tick.
    /// Distinct from StatusVisualEffect (persistent tint applied once at Start).
    /// </summary>
    public VisualEffect? TickVisualEffect { get; set; }

    private Timer _tickTimer;
    private Timer _durationTimer;

    public void Setup(float duration, float interval, ICombatEffect tickEffect, PackedScene? tickVisuals,
        PackedScene? persistentVisuals, IEnumerable<CombatTag> tags,
        VisualEffect? visualEffect = null, VisualEffect? tickVisualEffect = null)
    {
        Duration = duration;
        Interval = interval;
        TickEffect = tickEffect;
        TickVisuals = tickVisuals;
        PersistentVisuals = persistentVisuals;
        Tags = tags;
        StatusVisualEffect = visualEffect;
        TickVisualEffect = tickVisualEffect;
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

        // Duration <= 0 means "infinite" — tick timer runs until manually stopped
        // (by cleanse, death, etc). The duration timer simply doesn't start.

        // Setup Duration Timer
        if (Duration > 0)
        {
            _durationTimer.WaitTime = Duration;
            _durationTimer.Timeout += OnDurationExpired;
            _durationTimer.Start();
        }

        // Setup Tick Timer
        if (Interval > 0)
        {
            _tickTimer.WaitTime = Interval;
            _tickTimer.Timeout += OnTick;
            _tickTimer.Start();
        }

        // Fire the per-tick *visual* once at application time so the moment of
        // status onset is legible to the player. Damage application stays on the
        // Interval timer to preserve DPS — only the visual portion runs eagerly.
        PlayTickVisuals();
    }

    private void PlayTickVisuals()
    {
        if (TickVisualEffect != null)
        {
            VisualController?.PlayEffect(TickVisualEffect);
        }

        if (TickVisuals != null)
        {
            var visual = TickVisuals.Instantiate();
            if (visual is not Node3D)
            {
                Shared.JmoLogger.Warning(this, $"TickVisuals instantiated as {visual.GetType().Name}, expected Node3D");
            }
            Target.OwnerNode.AddChild(visual);
        }
    }

    private void OnTick()
    {
        PlayTickVisuals();

        if (TickEffect != null)
        {
            // Reissue the original impact context as Tick-kind so the per-tick visual
            // (e.g. burn-tint flash) isn't stacked with the generic damage hit-flash —
            // HitFlashComponent and similar primary-impact-only subscribers filter on Kind.
            TickEffect.Apply(Target, Context.WithKind(Jmodot.Core.Health.DamageKind.Tick));
        }
    }

    private void OnDurationExpired() => Stop();

    public override void Stop(bool wasDispelled = false)
    {
        // Timers are nullable until _Ready fires. Defensive guards against pre-Ready Stop
        // (factory rejection cleanup, externally-driven dispel before AddChild completes).
        if (_tickTimer != null)
        {
            _tickTimer.Timeout -= OnTick;
            _tickTimer.Stop();
        }
        if (_durationTimer != null)
        {
            _durationTimer.Timeout -= OnDurationExpired;
            _durationTimer.Stop();
        }
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
    /// <remarks>
    /// <para>Virtual so subclasses can rescale the refreshed duration (the incoming runner has
    /// never started, so its Duration carries no target-side scaling).</para>
    /// <para>
    /// OVERRIDE CONTRACT: the refresh is conditional even though <see cref="IDurationRefreshable"/>
    /// does not say so — only a source that carries a duration concept may drive
    /// <see cref="SetDuration"/>. An override MUST route through
    /// <see cref="TryGetSourceDuration"/> (or reproduce it exactly) and MUST leave the current
    /// duration untouched when it returns false; silently extending on an unhandled source shape
    /// is a behavior change, not a rescale. Overrides are expected to transform the resolved
    /// duration, not to change which sources are honored.
    /// </para>
    /// </remarks>
    public virtual void RefreshDuration(StatusRunner source)
    {
        if (TryGetSourceDuration(source, out var duration))
        {
            SetDuration(duration);
        }
        else
        {
            // The caller (StatusEffectComponent.RefreshOldestMatchingRunner → AddStatus) reports
            // success regardless, so an unhandled shape silently drops the incoming status.
            JmoLogger.Warning(this, $"[Status] RefreshDuration ignored source without a duration concept: {source?.GetType().Name ?? "null"}");
        }
    }

    /// <summary>
    /// Resolves the duration carried by a refresh source. Returns false when the source carries
    /// no duration concept — callers, including <see cref="RefreshDuration"/> overrides, must then
    /// leave the current duration untouched.
    /// </summary>
    protected static bool TryGetSourceDuration(StatusRunner source, out float duration)
    {
        if (source is TickStatusRunner tickSource)
        {
            duration = tickSource.Duration;
            return true;
        }

        if (source is DurationStatusRunner durationSource)
        {
            duration = durationSource.Duration;
            return true;
        }

        duration = 0f;
        return false;
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
