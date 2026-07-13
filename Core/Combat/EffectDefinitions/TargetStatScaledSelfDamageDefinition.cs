namespace Jmodot.Core.Combat.EffectDefinitions;

using Godot;
using Jmodot.Implementation.Shared;
using Stats;
using GCol = Godot.Collections;

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
    private BaseSelfDamageDefinition? _inner;

    /// <summary>
    /// The wrapped strategy providing the base damage (e.g. fraction-of-max-health).
    /// Null = 0 damage.
    /// NOT [Export] — serialization handled via _Set/_Get/_GetPropertyList to avoid
    /// InvalidCastException in the generated setter during [Tool] deserialization.
    /// </summary>
    public BaseSelfDamageDefinition? Inner
    {
        get => _inner;
        set => _inner = value;
    }

    /// <summary>Stat read from the COLLIDED entity (e.g. mass). Null = always TargetStatDefault.</summary>
    [Export] public Attribute? TargetStatAttr { get; set; }

    /// <summary>Target stat value when the target has no stats or the attribute is unwired.</summary>
    [Export] public float TargetStatDefault { get; set; } = 1f;

    /// <summary>Stat read from the HOST's own stats dividing the scale (e.g. pierce_power). Null = always DivisorStatDefault.</summary>
    [Export] public Attribute? DivisorStatAttr { get; set; }

    /// <summary>Divisor value when the host stat is unwired or absent.</summary>
    [Export] public float DivisorStatDefault { get; set; } = 1f;

    /// <summary>Upper clamp on the scale multiplier (also used when the divisor is ≤ 0).</summary>
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
        float baseDamage = _inner?.ResolveCollisionDamage(impactVelocity, stats, target) ?? 0f;
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

    // ─── Manual Serialization ───────────────────────────
    // Required for the polymorphic BaseSelfDamageDefinition field to avoid
    // InvalidCastException during [Tool] deserialization.

    public override Variant _Get(StringName property)
    {
        if (property == "Inner")
        {
            return _inner != null ? Variant.From((Resource)_inner) : default;
        }
        return default;
    }

    public override bool _Set(StringName property, Variant value)
    {
        if (property == "Inner")
        {
            _inner = value.AsGodotObject() as BaseSelfDamageDefinition;
            return true;
        }
        return false;
    }

    public override GCol.Array<GCol.Dictionary> _GetPropertyList()
    {
        return new GCol.Array<GCol.Dictionary>
        {
            new GCol.Dictionary
            {
                { "name", "Inner" },
                { "type", (int)Variant.Type.Object },
                { "hint", (int)PropertyHint.ResourceType },
                { "hint_string", "BaseSelfDamageDefinition" },
                { "usage", (int)(PropertyUsageFlags.Default | PropertyUsageFlags.Storage) }
            }
        };
    }
}
