namespace Jmodot.Implementation.Combat.Effects.StatusEffects;

using Jmodot.Core.Combat.Status;
using Jmodot.Implementation.Combat.Status;

/// <summary>
/// Shared apply-time wiring for status effects. Centralizes the spread-state handoff so a new
/// spread field lands in one place instead of being copied into every *Effect.Apply().
/// </summary>
public static class StatusEffectApplyHelper
{
    /// <summary>
    /// Copies the effect's spread state onto the runner it spawned: the runner inherits the
    /// SpreadConfig + generation, and SourceEffect lets the component's spread loop re-Apply
    /// this snapshot to a picked target.
    /// </summary>
    public static void WireSpread(StatusRunner runner, ISpreadAwareCombatEffect effect)
    {
        runner.SpreadConfig = effect.SpreadConfig;
        runner.SourceEffect = effect;
        runner.SpreadGeneration = effect.SpreadGeneration;
        runner.InjectStreamSeed(effect.NextStreamSeed);
    }
}
