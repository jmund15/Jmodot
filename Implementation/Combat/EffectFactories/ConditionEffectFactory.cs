// using Godot;
// using Jmodot.Core.Combat;
// using Jmodot.Implementation.Combat.Effects;
// using Jmodot.Implementation.Combat.Status;
// using GCol = Godot.Collections;
//
// namespace Jmodot.Implementation.Combat.EffectFactories;
//
// [GlobalClass]
// public partial class ConditionEffectFactory : CombatEffectFactory
// {
//     [Export] public StatusCondition Condition { get; set; }
//     [Export] public float CheckInterval { get; set; } = 0.1f;
//     [Export] public CombatEffectFactory EffectToApplyOnTick { get; set; }
//     [Export] public CombatEffectFactory EffectToApplyOnEnd { get; set; }
//     [Export] public GCol.Array<GameplayTag> Tags { get; set; } = [];
//     [Export] public PackedScene PersistentVisuals { get; set; }
//
//     public override ICombatEffect Create(Jmodot.Core.Stats.IStatProvider? stats = null)
//     {
//         return new ConditionStatusRunner
//         {
//             Condition = Condition,
//             CheckInterval = CheckInterval,
//             OnTickEffect = EffectToApplyOnTick?.Create(stats),
//             OnEndEffect = EffectToApplyOnEnd?.Create(stats),
//             Tags = Tags,
//             PersistentVisuals = PersistentVisuals
//         };
//     }
// }
