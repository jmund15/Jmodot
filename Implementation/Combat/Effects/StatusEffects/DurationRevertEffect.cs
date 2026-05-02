namespace Jmodot.Implementation.Combat.Effects.StatusEffects;

using System.Collections.Generic;
using Jmodot.Core.Combat;
using Jmodot.Core.Combat.Reactions;
using Jmodot.Core.Combat.Status;
using AI.BB;
using Combat;
using Core.Visual.Effects;
using Shared;
using Status;

/// <summary>
/// The "Instruction" to apply a Duration Effect.
/// Contains only raw data (the Snapshot). No logic, no Godot Nodes.
/// </summary>
public class DurationRevertEffect : ISpreadAwareCombatEffect
{
    public PackedScene Prefab { get; private init; }
    public float Duration { get; private init; }
    public IRevertibleCombatEffect RevertEffect { get; private init; }
    public PackedScene? PersistentVisuals { get; private init; }
    public IEnumerable<CombatTag> Tags { get; private init; }
    public VisualEffect? Visual { get; private init; }

    public Core.Combat.Status.StatusSpreadConfig? SpreadConfig { get; set; }
    public int SpreadGeneration { get; set; } = 0;

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

        // 2. Access Component (combined null check — TryGet=true with null value is the
        // null-storage asymmetry case; collapsing both checks here keeps step 5 clean).
        if (!target.Blackboard.TryGet<StatusEffectComponent>(BBDataSig.StatusEffects, out var statusComp)
            || statusComp == null)
        {
            return null;
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

        // 4a. Wire spread (if configured).
        runner.SpreadConfig = SpreadConfig;
        runner.SourceEffect = this;
        runner.SpreadGeneration = SpreadGeneration;

        // 5. Add to System (Component handles parenting and lifecycle management).
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
