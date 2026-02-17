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
/// The "Instruction" to apply a Duration Effect.
/// Contains only raw data (the Snapshot). No logic, no Godot Nodes.
/// </summary>
public class DurationRevertEffect : ICombatEffect
{
    public PackedScene Prefab { get; private init; }
    public float Duration { get; private init; }
    public IRevertibleCombatEffect RevertEffect { get; private init; }
    public PackedScene? PersistentVisuals { get; private init; }
    public IEnumerable<CombatTag> Tags { get; private init; }
    public VisualEffect? Visual { get; private init; }

    public DurationRevertEffect(
        PackedScene prefab,
        float duration,
        IRevertibleCombatEffect revertEffect,
        PackedScene? persistentVisuals,
        IEnumerable<CombatTag> tags,
        VisualEffect? visual = null)
    {
        Prefab = prefab;
        Duration = duration;
        RevertEffect = revertEffect;
        PersistentVisuals = persistentVisuals;
        Tags = tags;
        Visual = visual;
    }

    /// <summary>
    /// Executes the instruction: Spawns the Runner Node and adds it to the target.
    /// </summary>
    public CombatResult? Apply(ICombatant target, HitContext context)
    {
        // 1. Validation
        if (target == null || Prefab == null)
        {
            return null;
        }

        // 2. Access Component
        if (!target.Blackboard.TryGet<StatusEffectComponent>
                (BBDataSig.StatusEffects, out var statusComp))
        {
            return null; // Target cannot accept status effects
        }

        // 3. Instantiate the Runner (The Node)
        var prefabInst = Prefab.Instantiate();
        if (prefabInst is not DurationRevertibleStatusRunner runner)
        {
            JmoLogger.Error(this, $"Prefab {prefabInst.Name} is not a DurationRevertibleStatusRunner!");
            return null;
        }

        // 4. Inject the Snapshot Data
        runner.Setup(Duration, RevertEffect, PersistentVisuals, Tags, Visual);

        // 5. Add to System
        // The Component handles parenting and lifecycle management.
        bool wasAccepted = statusComp!.AddStatus(runner, target, context);

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
