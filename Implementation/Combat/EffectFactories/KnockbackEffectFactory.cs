namespace Jmodot.Implementation.Combat.EffectFactories;

using Godot;
using Jmodot.Core.Combat;
using Jmodot.Core.Shared.Attributes;
using Jmodot.Core.Stats;
using Jmodot.Implementation.Combat.Effects;

/// <summary>
/// Factory for KnockbackEffect Resource. Designer wires a KnockbackEffect sub-resource
/// into <c>_effect</c>; factory returns it directly. Wired-resource pattern (vs.
/// construct-fresh-per-call as in DamageEffectFactory) chosen because KnockbackEffect
/// has no per-cast snapshot — force resolves at Apply time, direction computes at Apply
/// time. The only mutable state on KnockbackEffect is a one-time-warning latch
/// (_curveDeferralWarned), which is race-tolerant. Sharing the same Resource across
/// concurrent CombatPayload consumers is safe.
/// </summary>
[GlobalClass]
public partial class KnockbackEffectFactory : CombatEffectFactory
{
    [Export, RequiredExport] private KnockbackEffect _effect = null!;

    public override ICombatEffect Create(IStatProvider? stats = null) => this._effect;

#if TOOLS
    internal void SetEffectForTesting(KnockbackEffect effect) => this._effect = effect;
#endif
}
