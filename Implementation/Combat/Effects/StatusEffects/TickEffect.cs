namespace Jmodot.Implementation.Combat.Effects.StatusEffects;

using System.Collections.Generic;
using Jmodot.Core.Combat;
using Jmodot.Core.Combat.Reactions;
using AI.BB;
using Combat;
using Status;

/// <summary>
/// The "Instruction" to apply a Tick Effect.
/// Contains only raw data (the Snapshot). No logic, no Godot Nodes.
/// </summary>
public class TickEffect : ICombatEffect
{
    public PackedScene Prefab { get; private init; }

    public float Duration { get; private init; }
    public float Interval { get; init; }
    public ICombatEffect PerTickEffect { get; private init; } = null!;
    public PackedScene? PersistantVisuals { get; private init; }
    public PackedScene? TickVisuals { get; private init; }

    public IEnumerable<CombatTag> Tags { get; private init; }

    public TickEffect(
        PackedScene prefab,
        float duration, float interval,
        ICombatEffect perTickEffect,
        PackedScene? tickVisuals, PackedScene? persistantVisuals,
        IEnumerable<CombatTag> tags)
    {
        Prefab = prefab;
        Duration = duration;
        Interval = interval;
        PerTickEffect = perTickEffect;
        TickVisuals = tickVisuals;
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
        if (!target.Blackboard.TryGet(BBDataSig.StatusEffects, out StatusEffectComponent statusComp))
        {
            return null; // Target cannot accept status effects
        }

        // 3. Instantiate the Runner (The Node)
        var prefabInst = Prefab.Instantiate();
        // Note: We cast to TickStatusRunner to access the specific Setup() method.
        if (prefabInst is not TickStatusRunner runner)
        {
            GD.PrintErr($"Prefab {prefabInst.Name} is not a TickStatusRunner!");
            return null;
        }

        // 4. Inject the Snapshot Data
        runner.Setup(Duration, Interval, PerTickEffect, TickVisuals, PersistantVisuals, Tags);

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
