namespace Jmodot.Implementation.Combat.Effects.StatusEffects;

using System.Collections.Generic;
using Jmodot.Core.Combat;
using Jmodot.Core.Combat.Reactions;
using AI.BB;
using Combat;
using Status;

/// <summary>
/// The "Instruction" to apply a Duration Effect.
/// Contains only raw data (the Snapshot). No logic, no Godot Nodes.
/// </summary>
public class DelayEffect : ICombatEffect
{
    public PackedScene Prefab { get; private init; }

    public float Delay { get; private init; }
    public ICombatEffect DelayedEffect { get; private init; }
    public PackedScene? PersistantVisuals { get; private init; }
    public IEnumerable<GameplayTag> Tags { get; private init; }

    public DelayEffect(
        PackedScene prefab,
        float delay,
        ICombatEffect delayedEffect,
        PackedScene? persistantVisuals,
        IEnumerable<GameplayTag> tags)
    {
        Prefab = prefab;
        Delay = delay;
        DelayedEffect = delayedEffect;
        PersistantVisuals = persistantVisuals;
        Tags = tags;
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
        // Note: We cast to TickStatusRunner to access the specific Setup() method.
        if (prefabInst is not DelayedStatusRunner runner)
        {
            GD.PrintErr($"Prefab {prefabInst.Name} is not a TickStatusRunner!");
            return null;
        }

        // 4. Inject the Snapshot Data
        runner.Setup(Delay, DelayedEffect, PersistantVisuals, Tags);

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
