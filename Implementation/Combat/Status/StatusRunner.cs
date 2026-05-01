 using Godot;
using System;
using Jmodot.Core.Combat;
using Jmodot.Core.Combat.Status;

namespace Jmodot.Implementation.Combat.Status;

using System.Collections.Generic;
using System.Linq;
using AI.BB;
using Core.Combat.Reactions;
using Core.Combat.Status;
using Core.Visual.Effects;
using Implementation.Visual.Effects;
using Shared;

/// <summary>
/// Base class for all runtime status logic.
/// These are Nodes that exist as children of the StatusEffectComponent.
/// They now implement ICombatEffect directly to allow Factories to return them.
/// </summary>
public abstract partial class StatusRunner : Node
{
    // Fired when this runner is done (for any reason).
    // The StatusComponent listens to this to clean up.
    // wasDispelled: true = manually stopped/dispelled, false = completed naturally
    public event Action<StatusRunner, bool> OnStatusFinished = delegate { };

    /// <summary>
    /// Tags associated with this status (e.g., "Stun", "Poison", "Buff").
    /// Used by the StatusEffectComponent to track active states.
    /// </summary>
    public IEnumerable<CombatTag> Tags { get; protected set; } = [];
    /// <summary>
    /// Optional visual scene to spawn and hold for the duration of the status.
    /// </summary>
    public PackedScene? PersistentVisuals { get; protected set; }
    
    /// <summary>
    /// Optional visual effect (tint, flash, shader) to apply to the target during the status.
    /// </summary>
    public VisualEffect? StatusVisualEffect { get; set; }

    /// <summary>
    /// Optional spread configuration. When non-null, the StatusEffectComponent's spread-evaluation
    /// timer ticks this runner: every <see cref="StatusSpreadConfig.TryEvaluate"/> call may spawn
    /// a sibling instance of the same status type on a nearby qualifying target.
    /// Spread is a base capability of every status — burn, stun, rage, etc. all opt in by Resource.
    /// </summary>
    [Export] public StatusSpreadConfig? SpreadConfig { get; set; }

    /// <summary>
    /// The spread generation of this runner. 0 = primary application; 1+ = spread-spawned generations.
    /// Bumped by the component when it spawns a spread sibling. Read by SpreadConfig.TryEvaluate
    /// for the generation-gate and falloff curve.
    /// </summary>
    public int SpreadGeneration { get; set; } = 0;

    /// <summary>
    /// The ICombatEffect snapshot that spawned this runner. Used by the spread-evaluation
    /// path in StatusEffectComponent to spawn fresh sibling runners on nearby targets — calling
    /// SourceEffect.Apply(pickedTarget, ...) reuses the resolved values (Duration, Interval, …)
    /// without needing the original spell's StatProvider.
    /// </summary>
    public ICombatEffect? SourceEffect { get; set; }

    /// <summary>
    /// Per-runner accumulator (seconds) advanced each frame by the StatusEffectComponent's
    /// spread driver. When it crosses <see cref="StatusSpreadConfig.EvaluationInterval"/> the
    /// driver triggers an evaluation and decrements (preserving overshoot so cadence doesn't
    /// drift on slow frames).
    /// </summary>
    public float SpreadEvalAccumulator { get; internal set; }

    /// <summary>
    /// Total spread evaluations attempted on this runner. Compared against
    /// <see cref="StatusSpreadConfig.MaxEvaluations"/> via <see cref="StatusSpreadConfig.CanEvaluate"/>
    /// to short-circuit the loop once the budget is exhausted.
    /// </summary>
    public int SpreadEvaluationCount { get; internal set; }

    private bool _stopped;
    private Node? _visualInstance;
    protected VisualEffectController? VisualController { get; private set; }

    /// <summary>
    /// HitContext captured at <see cref="Start"/> time. Public read for the spread-evaluation loop
    /// (component reuses it when re-applying SourceEffect on a picked target).
    /// </summary>
    public HitContext Context { get; private set; }

    /// <summary>
    /// Target combatant captured at <see cref="Start"/> time. Public read for the spread loop
    /// (component reads target.OwnerNode for the spatial query origin).
    /// </summary>
    public ICombatant Target { get; private set; }


    /// <summary>
    /// ICombatEffect Implementation.
    /// Cancels the effect.
    /// </summary>
    public void Cancel()
    {
        Stop(true);
    }
    public virtual void Start(ICombatant target, HitContext context)
    {
        Target = target;
        Context = context;

        if (PersistentVisuals != null)
        {
            _visualInstance = PersistentVisuals.Instantiate();

            // TODO: add config for if visuals should be parented to the target or the status effect component
            target.OwnerNode.AddChild(_visualInstance);
        }

        // Resolve once for the lifetime of the runner — subclasses (e.g. TickStatusRunner)
        // also drive per-tick effects through the same controller, so the lookup must
        // succeed even when StatusVisualEffect is null.
        VisualController = FindVisualController(target);

        if (StatusVisualEffect != null)
        {
            VisualController?.PlayEffect(StatusVisualEffect);
        }
        
        // Subclasses implement specific logic (Timers, Visuals)
    }

    /// <summary>
    /// Called when the status is removed or finished.
    /// </summary>
    /// <param name="wasDispelled"></param>
    public virtual void Stop(bool wasDispelled = false)
    {
        if (_stopped) { return; }
        _stopped = true;

        if (_visualInstance != null && IsInstanceValid(_visualInstance))
        {
            _visualInstance.QueueFree();
            _visualInstance = null;
        }

        if (StatusVisualEffect != null && VisualController != null && IsInstanceValid(VisualController))
        {
            VisualController.StopEffect(StatusVisualEffect);
        }

        OnStatusFinished?.Invoke(this, wasDispelled);
        QueueFree();
    }
    
    private VisualEffectController? FindVisualController(ICombatant target)
    {
        if (target?.OwnerNode == null) { return null; }
        
        // Try to find in children
        var controller = target.OwnerNode.GetChildrenOfType<VisualEffectController>().FirstOrDefault();
        return controller;
    }
}
