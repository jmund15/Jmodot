namespace Jmodot.Core.Combat;

/// <summary>
/// Selects WHEN a damage effect rolls its critical hit, picked by the assembling hitbox from its
/// continuous/one-shot configuration.
/// <list type="bullet">
/// <item><see cref="Resolved"/> — roll at effect-assembly time (one roll covers the whole swing;
/// all targets of a single attack crit together). Behavior-preserving for standard attacks.</item>
/// <item><see cref="DeferredPerHit"/> — defer the roll to <c>ICombatEffect.Apply</c>, derived from the
/// per-hit <c>HitContext.HitSeed</c>, so a continuous hitbox rolls crit independently per tick.</item>
/// </list>
/// </summary>
public enum CritResolution
{
    Resolved,
    DeferredPerHit,
}
