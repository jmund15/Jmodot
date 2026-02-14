namespace Jmodot.Core.Combat.EffectDefinitions;

using Godot;
using Stats;
using GCol = Godot.Collections;

/// <summary>
/// A BaseFloatValueDefinition that calculates damage from impact velocity
/// with an optional flat/stat-driven base component.
///
/// Supports three designer configurations:
/// - BaseDamageDefinition only (Profile=null): flat or stat-driven damage per collision
/// - Profile only (BaseDamageDefinition=null): pure velocity-scaled damage
/// - Both: guaranteed base + velocity bonus
///
/// The DamageMultiplier is applied AFTER the profile's MaxDamage cap,
/// scaling only the velocity component, not the base.
/// </summary>
[GlobalClass, Tool]
public partial class ImpactDamageDefinition : BaseFloatValueDefinition
{
    private BaseFloatValueDefinition? _baseDamageDefinition;

    /// <summary>
    /// The profile that calculates velocity-based damage.
    /// Supports linear and curve-based modes.
    /// </summary>
    [Export] public ImpactDamageProfile? Profile { get; set; }

    /// <summary>
    /// Multiplier applied after the profile calculates velocity damage.
    /// Use for per-entity scaling (e.g., heavy ingredients take more self-damage).
    /// Applied AFTER the profile's MaxDamage cap, to the velocity component only.
    /// </summary>
    [Export] public float DamageMultiplier { get; set; } = 1.0f;

    /// <summary>
    /// Optional flat/stat-driven base damage added to velocity damage.
    /// Resolved independently of velocity context via ResolveFloatValue(stats).
    /// Use ConstantFloatDefinition for a flat floor, AttributeFloatDefinition for stat-driven.
    /// NOT [Export] — serialization handled via _Set/_Get/_GetPropertyList to avoid
    /// InvalidCastException in the generated setter during [Tool] deserialization.
    /// </summary>
    public BaseFloatValueDefinition? BaseDamageDefinition
    {
        get => _baseDamageDefinition;
        set => _baseDamageDefinition = value;
    }

    /// <summary>
    /// Returns the base damage portion (without velocity context).
    /// Returns 0 if no BaseDamageDefinition is set (backward compatible).
    /// </summary>
    public override float ResolveFloatValue(IStatProvider? stats)
        => _baseDamageDefinition?.ResolveFloatValue(stats) ?? 0f;

    /// <summary>
    /// Resolves total damage: base + velocity-scaled component.
    /// Called by the collision runner when it detects this definition type.
    /// </summary>
    /// <param name="impactVelocity">The velocity magnitude at impact (always positive).</param>
    /// <param name="stats">Optional stat provider for resolving BaseDamageDefinition.</param>
    /// <returns>BaseDamage + (Profile damage * DamageMultiplier).</returns>
    public float ResolveWithVelocity(float impactVelocity, IStatProvider? stats = null)
    {
        float baseDamage = _baseDamageDefinition?.ResolveFloatValue(stats) ?? 0f;

        float velocityDamage = Profile != null
            ? Profile.CalculateDamage(impactVelocity) * DamageMultiplier
            : 0f;

        return baseDamage + velocityDamage;
    }

    // ─── Manual Serialization ───────────────────────────
    // Required for polymorphic BaseFloatValueDefinition field to avoid
    // InvalidCastException during [Tool] deserialization (same pattern as DurableCollisionResponse).

    public override Variant _Get(StringName property)
    {
        if (property == "BaseDamageDefinition")
        {
            return _baseDamageDefinition != null ? Variant.From((Resource)_baseDamageDefinition) : default;
        }
        return default;
    }

    public override bool _Set(StringName property, Variant value)
    {
        if (property == "BaseDamageDefinition")
        {
            _baseDamageDefinition = value.AsGodotObject() as BaseFloatValueDefinition;
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
                { "name", "BaseDamageDefinition" },
                { "type", (int)Variant.Type.Object },
                { "hint", (int)PropertyHint.ResourceType },
                { "hint_string", "BaseFloatValueDefinition" },
                { "usage", (int)(PropertyUsageFlags.Default | PropertyUsageFlags.Storage) }
            }
        };
    }
}
