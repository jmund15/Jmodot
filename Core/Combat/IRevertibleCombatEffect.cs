namespace Jmodot.Core.Combat;

using System;

public interface IRevertibleCombatEffect : ICombatEffect
{
    /// <summary>
    ///
    /// </summary>
    /// <param name="target"></param>
    /// <param name="context"></param>
    /// <returns>True if succesfully reverted, false if not (means effect wasn't applied)</returns>
    bool TryRevert(ICombatant target, HitContext context);
    event Action<IRevertibleCombatEffect> EffectReverted;
}
