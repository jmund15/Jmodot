using Godot;
using Jmodot.Core.Combat;
using Jmodot.Implementation.Combat.Effects;

namespace Jmodot.Implementation.Combat.EffectFactories;

[GlobalClass]
public partial class ConditionEffectFactory : CombatEffectFactory
{
    [Export] public StatusCondition Condition { get; set; }
    [Export] public float CheckInterval { get; set; } = 0.1f;
    [Export] public CombatEffectFactory EffectToApplyOnTick { get; set; }
    [Export] public CombatEffectFactory EffectToApplyOnEnd { get; set; }
    [Export] public string[] Tags { get; set; } = System.Array.Empty<string>();

    public override ICombatEffect Create()
    {
        return new ConditionEffect(Condition, CheckInterval, EffectToApplyOnTick, EffectToApplyOnEnd, Tags);
    }
}
