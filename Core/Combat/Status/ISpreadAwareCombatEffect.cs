namespace Jmodot.Core.Combat.Status;

/// <summary>
/// Marker interface for ICombatEffect snapshot types that can be re-applied to spawn a
/// spread sibling runner. Allows <see cref="Implementation.Combat.StatusEffectComponent"/>'s
/// spread-evaluation loop to set the next-generation count and re-Apply on a picked target,
/// without needing to know the concrete effect type or re-resolve attribute-driven values
/// through the factory.
/// </summary>
public interface ISpreadAwareCombatEffect : ICombatEffect
{
    /// <summary>
    /// The spread configuration carried by this effect snapshot. When set, the spawned
    /// runner inherits it and the component's spread loop can pick this runner up.
    /// </summary>
    StatusSpreadConfig? SpreadConfig { get; set; }

    /// <summary>
    /// Generation to stamp on the runner when this effect spawns one. Drives the spread
    /// generation gate + falloff curve. Default 0 = primary application.
    /// </summary>
    int SpreadGeneration { get; set; }
}
