namespace Jmodot.Implementation.Combat.Effects.StatusEffects;

using System.Collections.Generic;
using Jmodot.Core.Combat;
using Jmodot.Core.Combat.Reactions;
using AI.BB;
using Combat;
using Core.Visual.Effects;
using Shared;
using Status;

/// <summary>
/// The "Instruction" to apply a Tick Effect.
/// Contains only raw data (the Snapshot). No logic, no Godot Nodes.
/// </summary>
public class TickEffect : ICombatEffect
{
    public PackedScene Prefab { get; private init; }

    public float Duration { get; set; }
    public float Interval { get; init; }
    public ICombatEffect PerTickEffect { get; private init; } = null!;
    public PackedScene? PersistentVisuals { get; private init; }
    public PackedScene? TickVisuals { get; private init; }

    public IEnumerable<CombatTag> Tags { get; private init; }
    public VisualEffect? Visual { get; private init; }

    /// <summary>
    /// Optional per-tick visual effect (flash/pulse) distinct from the persistent StatusVisualEffect.
    /// </summary>
    public VisualEffect? TickVisualEffect { get; private init; }

    public TickEffect(
        PackedScene prefab,
        float duration, float interval,
        ICombatEffect perTickEffect,
        PackedScene? tickVisuals, PackedScene? persistentVisuals,
        IEnumerable<CombatTag> tags,
        VisualEffect? visual = null,
        VisualEffect? tickVisualEffect = null)
    {
        Prefab = prefab;
        Duration = duration;
        Interval = interval;
        PerTickEffect = perTickEffect;
        TickVisuals = tickVisuals;
        PersistentVisuals = persistentVisuals;
        Tags = tags;
        Visual = visual;
        TickVisualEffect = tickVisualEffect;
    }

    /// <summary>
    /// Executes the instruction: Spawns the Runner Node and adds it to the target.
    /// </summary>
    public CombatResult? Apply(ICombatant target, HitContext context)
    {
        // 1. Validation — explicit, logged guards. Silent returns hide configuration bugs
        // (e.g. health_tier_1.tres shipped with a missing Runner field undetected for weeks).
        if (target == null)
        {
            JmoLogger.Error(this, "TickEffect.Apply: target is null");
            return null;
        }
        if (Prefab == null)
        {
            JmoLogger.Error(this, "TickEffect.Apply: Prefab (Runner) is null — factory misconfigured. Use [Export, RequiredExport] PackedScene Runner on the originating CombatEffectFactory and ensure the .tres file assigns it.");
            return null;
        }

        // 2. Access Component
        if (!target.Blackboard.TryGet(BBDataSig.StatusEffects, out StatusEffectComponent statusComp) || statusComp == null)
        {
            JmoLogger.Warning(this, $"TickEffect.Apply: target '{target.GetUnderlyingNode()?.Name}' has no StatusEffectComponent on its Blackboard.");
            return null; // Target cannot accept status effects
        }

        // 3. Instantiate the Runner (The Node)
        var prefabInst = Prefab.Instantiate();
        if (prefabInst is not TickStatusRunner runner)
        {
            JmoLogger.Error(this, $"Prefab {prefabInst.Name} is not a TickStatusRunner!");
            return null;
        }

        // 4. Inject the Snapshot Data
        runner.Setup(Duration, Interval, PerTickEffect, TickVisuals, PersistentVisuals, Tags, Visual, TickVisualEffect);

        // 5. Add to System
        // The Component handles parenting and lifecycle management.
        bool wasAccepted = statusComp.AddStatus(runner, target, context);

        if (!wasAccepted)
        {
            runner.QueueFree();
            return null;
        }

        // 6. Return the Result
        return new StatusResult
        {
            Source = context.Source,
            Target = target.OwnerNode,
            Tags = Tags,
            Runner = runner
        };
    }
}
