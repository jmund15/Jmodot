using Godot;
using Jmodot.Core.Combat;

namespace Jmodot.Implementation.Combat.Status;

using System.Collections.Generic;

public partial class TickStatusRunner : StatusRunner
{
    private readonly float _duration;
    private readonly float _interval;
    private readonly ICombatEffect _effect;

    private Timer _tickTimer;
    private Timer _durationTimer;

    public TickStatusRunner(float duration, float interval, ICombatEffect effect, IEnumerable<GameplayTag> tags)
    {
        // INITIALIZE
        _duration = duration;
        _interval = interval;
        _effect = effect;
    }

    public override void Start()
    {
        base.Start();

        // Setup Duration Timer
        if (_duration > 0)
        {
            _durationTimer = new Timer();
            _durationTimer.WaitTime = _duration;
            _durationTimer.OneShot = true;
            _durationTimer.Timeout += Stop;
            AddChild(_durationTimer);
            _durationTimer.Start();
        }

        // Setup Tick Timer
        if (_interval > 0 && _effect != null)
        {
            _tickTimer = new Timer();
            _tickTimer.WaitTime = _interval;
            _tickTimer.OneShot = false;
            _tickTimer.Timeout += OnTick;
            AddChild(_tickTimer);
            _tickTimer.Start();
        }
    }

    /// <summary>
    /// Optional visual scene to spawn on each tick.
    /// </summary>
    public PackedScene TickVisuals { get; set; }

    private void OnTick()
    {
        // Spawn Visuals
        if (TickVisuals != null)
        {
            var visual = TickVisuals.Instantiate();
            // Add to target or self? Usually target root or self if self is attached to target.
            // Self is attached to StatusEffectComponent, which is on the Entity.
            // If we add to self, it might move with entity.
            // If visuals are particles, they might need to be independent if one-shot.
            // For now, add to self.
            AddChild(visual);
        }

        if (_effect != null)
        {
            _effect.Apply(Target, Context);
        }
    }

    public override void Stop()
    {
        if (_tickTimer != null) _tickTimer.Stop();
        if (_durationTimer != null) _durationTimer.Stop();
        base.Stop();
    }
}
