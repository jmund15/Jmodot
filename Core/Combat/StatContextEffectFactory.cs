namespace Jmodot.Core.Combat;

using Stats;

/// <summary>
/// Base class for combat effect factories that require stat context.
/// These factories create effects with values resolved from an IStatProvider at creation time.
/// This is a separate hierarchy from CombatEffectFactory to keep base factories pristine.
/// </summary>
[GlobalClass]
public abstract partial class StatContextEffectFactory : Resource
{
    public abstract ICombatEffect Create(IStatProvider stats);
}
