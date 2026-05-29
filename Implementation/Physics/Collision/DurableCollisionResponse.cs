namespace Jmodot.Implementation.Physics.Collision;

using Godot;
using Jmodot.Core.Combat.EffectDefinitions;
using Jmodot.Core.Stats;
using GCol = Godot.Collections;

/// <summary>
/// Abstract base for collision responses where the spell survives.
/// Provides durability (count limits, self-damage, fallback chains)
/// and physics configuration (velocity retention).
/// Concrete subclasses (Bounce, Pierce, Slide, Ignore) act as type discriminators —
/// the runner dispatches physics behavior based on the concrete type.
/// </summary>
[Tool]
[GlobalClass]
public abstract partial class DurableCollisionResponse : BaseCollisionResponse
{
    private BaseCollisionResponse? _fallbackResponse;
    private BaseSelfDamageDefinition? _selfDamageDefinition;
    private BaseIntValueDefinition? _maxCountDefinition;

    [ExportGroup("Durability")]
    /// <summary>
    /// Defines how self-damage is calculated per collision.
    /// Use SimpleSelfDamageDefinition for flat/stat-driven, ImpactSelfDamageDefinition for velocity-based.
    /// Null = no self-damage (0).
    /// NOT [Export] — serialization handled via _Set/_Get/_GetPropertyList to avoid
    /// InvalidCastException in the generated setter during [Tool] deserialization.
    /// </summary>
    public BaseSelfDamageDefinition? SelfDamageDefinition
    {
        get => _selfDamageDefinition;
        set => _selfDamageDefinition = value;
    }

    /// <summary>
    /// Defines how max count is calculated.
    /// Use ConstantIntDefinition for fixed values, AttributeIntDefinition for stat-driven.
    /// Null = unlimited (-1).
    /// NOT [Export] — serialization handled via _Set/_Get/_GetPropertyList to avoid
    /// InvalidCastException in the generated setter during [Tool] deserialization.
    /// </summary>
    public BaseIntValueDefinition? MaxCountDefinition
    {
        get => _maxCountDefinition;
        set => _maxCountDefinition = value;
    }

    [ExportGroup("Physics")]
    /// <summary>Velocity multiplier after this collision (0.0–1.0).</summary>
    [Export(PropertyHint.Range, "0,1")] public float VelocityRetention { get; set; } = 1.0f;

    /// <summary>
    /// Minimum impact velocity required to process this collision.
    /// Below this threshold, the collision is ignored (no count consumed, no physics, no self-damage).
    /// 0 = no filtering (default).
    /// </summary>
    [Export(PropertyHint.Range, "0,50")] public float MinVelocityThreshold { get; set; } = 0f;

    /// <summary>
    /// When impact speed drops below this threshold, activate FallbackResponse instead of
    /// processing the response normally. Same activation mechanism as count exhaustion —
    /// the swap is sticky per-mapping for the lifetime of the spell.
    ///
    /// Enables velocity-driven mode transitions like Bounce → Slide as a spell slows.
    /// 0 = disabled (default). Requires FallbackResponse to be set; otherwise no-op.
    /// Distinct from MinVelocityThreshold (which rejects the contact entirely without
    /// activating fallback).
    /// </summary>
    [Export(PropertyHint.Range, "0,50,0.1")] public float VelocityFallbackThreshold { get; set; } = 0f;

    [ExportGroup("Fallback")]
    /// <summary>
    /// When MaxCount is exhausted, use this response instead of destroying.
    /// Enables patterns like bounce->slide, bounce->destroy, pierce->persist, etc.
    /// NOT [Export] — serialization handled via _Set/_Get/_GetPropertyList to avoid
    /// InvalidCastException in the generated setter during [Tool] deserialization.
    /// </summary>
    public BaseCollisionResponse? FallbackResponse
    {
        get => _fallbackResponse;
        set => _fallbackResponse = value;
    }

    // ─── Resolution Helpers ─────────────────────────────

    /// <summary>
    /// Resolves the max count value using the definition.
    /// Returns -1 (unlimited) if no definition is set.
    /// </summary>
    public int ResolveMaxCount(IStatProvider? stats)
        => _maxCountDefinition?.ResolveIntValue(stats) ?? -1;

    // ─── Manual Serialization ───────────────────────────

    public override Variant _Get(StringName property)
    {
        if (property == "FallbackResponse")
        {
            return _fallbackResponse != null ? Variant.From((Resource)_fallbackResponse) : default;
        }
        if (property == "SelfDamageDefinition")
        {
            return _selfDamageDefinition != null ? Variant.From((Resource)_selfDamageDefinition) : default;
        }
        if (property == "MaxCountDefinition")
        {
            return _maxCountDefinition != null ? Variant.From((Resource)_maxCountDefinition) : default;
        }
        return default;
    }

    public override bool _Set(StringName property, Variant value)
    {
        if (property == "FallbackResponse")
        {
            _fallbackResponse = value.AsGodotObject() as BaseCollisionResponse;
            return true;
        }
        if (property == "SelfDamageDefinition")
        {
            _selfDamageDefinition = value.AsGodotObject() as BaseSelfDamageDefinition;
            return true;
        }
        if (property == "MaxCountDefinition")
        {
            _maxCountDefinition = value.AsGodotObject() as BaseIntValueDefinition;
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
                { "name", "SelfDamageDefinition" },
                { "type", (int)Variant.Type.Object },
                { "hint", (int)PropertyHint.ResourceType },
                { "hint_string", "BaseSelfDamageDefinition" },
                { "usage", (int)(PropertyUsageFlags.Default | PropertyUsageFlags.Storage) }
            },
            new GCol.Dictionary
            {
                { "name", "MaxCountDefinition" },
                { "type", (int)Variant.Type.Object },
                { "hint", (int)PropertyHint.ResourceType },
                { "hint_string", "BaseIntValueDefinition" },
                { "usage", (int)(PropertyUsageFlags.Default | PropertyUsageFlags.Storage) }
            },
            new GCol.Dictionary
            {
                { "name", "FallbackResponse" },
                { "type", (int)Variant.Type.Object },
                { "hint", (int)PropertyHint.ResourceType },
                { "hint_string", "BaseCollisionResponse" },
                { "usage", (int)(PropertyUsageFlags.Default | PropertyUsageFlags.Storage) }
            }
        };
    }
}
