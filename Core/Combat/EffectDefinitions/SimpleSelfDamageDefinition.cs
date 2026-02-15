namespace Jmodot.Core.Combat.EffectDefinitions;

using Godot;
using Stats;
using GCol = Godot.Collections;

/// <summary>
/// A self-damage strategy that wraps a BaseFloatValueDefinition and ignores velocity.
/// Use for flat (ConstantFloatDefinition) or stat-driven (AttributeFloatDefinition) damage per collision.
/// </summary>
[GlobalClass, Tool]
public partial class SimpleSelfDamageDefinition : BaseSelfDamageDefinition
{
    private BaseFloatValueDefinition? _damageDefinition;

    /// <summary>
    /// The inner definition that provides the damage value.
    /// Null = 0 damage.
    /// NOT [Export] — serialization handled via _Set/_Get/_GetPropertyList to avoid
    /// InvalidCastException in the generated setter during [Tool] deserialization.
    /// </summary>
    public BaseFloatValueDefinition? DamageDefinition
    {
        get => _damageDefinition;
        set => _damageDefinition = value;
    }

    public SimpleSelfDamageDefinition() { }

    public SimpleSelfDamageDefinition(BaseFloatValueDefinition definition)
    {
        _damageDefinition = definition;
    }

    public override float ResolveCollisionDamage(float impactVelocity, IStatProvider? stats)
        => _damageDefinition?.ResolveFloatValue(stats) ?? 0f;

    // ─── Manual Serialization ───────────────────────────
    // Required for polymorphic BaseFloatValueDefinition field to avoid
    // InvalidCastException during [Tool] deserialization.

    public override Variant _Get(StringName property)
    {
        if (property == "DamageDefinition")
        {
            return _damageDefinition != null ? Variant.From((Resource)_damageDefinition) : default;
        }
        return default;
    }

    public override bool _Set(StringName property, Variant value)
    {
        if (property == "DamageDefinition")
        {
            _damageDefinition = value.AsGodotObject() as BaseFloatValueDefinition;
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
                { "name", "DamageDefinition" },
                { "type", (int)Variant.Type.Object },
                { "hint", (int)PropertyHint.ResourceType },
                { "hint_string", "BaseFloatValueDefinition" },
                { "usage", (int)(PropertyUsageFlags.Default | PropertyUsageFlags.Storage) }
            }
        };
    }
}
