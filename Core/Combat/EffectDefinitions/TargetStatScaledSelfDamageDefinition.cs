namespace Jmodot.Core.Combat.EffectDefinitions;

using Godot;
using Jmodot.Implementation.Shared;
using Stats;

/// <summary>
/// Decorator self-damage strategy: resolves an inner definition's damage, then scales it
/// by (target's stat ÷ host's divisor stat) — the float-based pierce-cost model. A heavy
/// target (high mass) multiplies the pierce cost; high pierce power divides it; a
/// heavy-enough target costs the host its remaining health and stops it outright.
///
/// Both stats are optional exports: an unresolvable target stat falls back to
/// <see cref="TargetStatDefault"/> (so stat-less props behave like an average target),
/// and a missing divisor falls back to <see cref="DivisorStatDefault"/>.
/// </summary>
[GlobalClass, Tool]
public partial class TargetStatScaledSelfDamageDefinition : BaseSelfDamageDefinition
{
    /// <summary>
    /// The wrapped strategy providing the base damage (e.g. fraction-of-max-health). Null = 0 damage.
    /// A plain typed [Export] — the whole self-damage family is [Tool], so the generated setter's
    /// cast resolves without the manual _Set/_Get serialization the cascade used to require.
    /// </summary>
    [Export] public BaseSelfDamageDefinition? Inner { get; set; }

    /// <summary>Stat read from the COLLIDED entity (e.g. mass) that scales the cost UP. Null = always TargetStatDefault.</summary>
    [ExportGroup("Target Stat (numerator)")]
    [Export] public Attribute? TargetStatAttr { get; set; }

    /// <summary>Target stat value when the target has no stats or the attribute is unwired.</summary>
    [Export] public float TargetStatDefault { get; set; } = 1f;

    /// <summary>Stat read from the HOST's own stats that scales the cost DOWN (e.g. pierce_power). Null = always DivisorStatDefault.</summary>
    [ExportGroup("Divisor Stat (denominator)")]
    [Export] public Attribute? DivisorStatAttr { get; set; }

    /// <summary>Divisor value when the host stat is unwired or absent.</summary>
    [Export] public float DivisorStatDefault { get; set; } = 1f;

    /// <summary>Upper clamp on the scale multiplier (also used when the divisor is ≤ 0).</summary>
    [ExportGroup("Clamp")]
    [Export] public float MaxMultiplier { get; set; } = 10f;

    /// <summary>
    /// Pure seam: damage multiplier = clamp(targetStat / divisorStat, 0, max).
    /// A non-positive divisor clamps to max (infinite pierce cost intent);
    /// a non-positive target stat clamps to 0 (free pierce).
    /// </summary>
    public static float ComputeScaleMultiplier(float targetStat, float divisorStat, float maxMultiplier)
    {
        if (targetStat <= 0f) { return 0f; }
        if (divisorStat <= 0f) { return maxMultiplier; }

        return Mathf.Clamp(targetStat / divisorStat, 0f, maxMultiplier);
    }

    public override float ResolveCollisionDamage(float impactVelocity, IStatProvider? stats)
        => ResolveCollisionDamage(impactVelocity, stats, null);

    public override float ResolveCollisionDamage(float impactVelocity, IStatProvider? stats, Node? target)
    {
        float baseDamage = Inner?.ResolveCollisionDamage(impactVelocity, stats, target) ?? 0f;
        if (baseDamage <= 0f) { return 0f; }

        float targetStat = ResolveTargetStat(target);
        float divisor = DivisorStatAttr != null
            ? stats?.GetStatValue<float>(DivisorStatAttr, DivisorStatDefault) ?? DivisorStatDefault
            : DivisorStatDefault;

        return baseDamage * ComputeScaleMultiplier(targetStat, divisor, MaxMultiplier);
    }

    private float ResolveTargetStat(Node? target)
    {
        if (TargetStatAttr == null || target == null) { return TargetStatDefault; }

        var provider = target as IStatProvider;
        if (provider == null
            && (!target.TryGetFirstChildOfInterface<IStatProvider>(out provider) || provider == null))
        {
            return TargetStatDefault;
        }

        return provider.GetStatValue<float>(TargetStatAttr, TargetStatDefault);
    }
}
