namespace Jmodot.Core.Combat;

using System;
using Reactions;

public interface IRevertibleCombatEffect : ICombatEffect
{
    /// <summary>
    ///
    /// </summary>
    /// <param name="target"></param>
    /// <param name="context"></param>
    /// <returns></returns>
    ICombatEffect? GetRevertEffect(ICombatant target, HitContext context);
}
