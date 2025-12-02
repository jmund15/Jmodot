namespace Jmodot.Core.Combat;

public interface ICombatEffectFactory
{
    ICombatEffect Create(Jmodot.Core.Stats.IStatProvider? stats = null);
}
