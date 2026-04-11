namespace Jmodot.Implementation.Combat.EffectFactories;

using Core.Combat;
using Core.Combat.EffectDefinitions;
using Core.Shared.Attributes;
using Core.Stats;
using Effects;

/// <summary>
/// Factory that creates a HealEffect restoring a percentage of max health.
/// Used by TickEffectFactory for health-over-time (regen) player effects.
/// </summary>
[GlobalClass]
public partial class HealEffectFactory : CombatEffectFactory
{
    [Export, RequiredExport]
    public BaseFloatValueDefinition HealPercent { get; private set; } = null!;

    public override ICombatEffect Create(IStatProvider? stats = null)
    {
        float percent = HealPercent.ResolveFloatValue(stats);
        return new HealEffect(percent, TargetVisualEffect);
    }
}
