namespace Jmodot.Implementation.Physics.Collision;

using Godot;
using Jmodot.Core.Identification;
using GCol = Godot.Collections;

/// <summary>
/// Maps one or more Categories to a collision response.
/// When a collider's Identity contains ANY of the MatchCategories,
/// this mapping's Response is used.
/// </summary>
[Tool]
[GlobalClass]
public partial class CategoryResponseMapping : Resource
{
    private GCol.Array<Category> _matchCategories = new();
    private BaseCollisionResponse? _response;

    /// <summary>
    /// Categories that trigger this response. Matches if the collider has ANY of these.
    /// </summary>
    [Export]
    public GCol.Array<Category> MatchCategories
    {
        get => _matchCategories;
        set => _matchCategories = value;
    }

    /// <summary>
    /// The collision response to apply when a match is found.
    /// NOT [Export] — serialization is handled via _Set/_Get/_GetPropertyList to avoid
    /// InvalidCastException in the generated setter during [Tool] deserialization.
    /// </summary>
    public BaseCollisionResponse? Response
    {
        get => _response;
        set => _response = value;
    }

    // ─── Manual Serialization for Response ───────────

    public override Variant _Get(StringName property)
    {
        if (property == "Response")
        {
            return _response != null ? Variant.From((Resource)_response) : default;
        }
        return default;
    }

    public override bool _Set(StringName property, Variant value)
    {
        if (property == "Response")
        {
            _response = value.AsGodotObject() as BaseCollisionResponse;
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
                { "name", "Response" },
                { "type", (int)Variant.Type.Object },
                { "hint", (int)PropertyHint.ResourceType },
                { "hint_string", "BaseCollisionResponse" },
                { "usage", (int)(PropertyUsageFlags.Default | PropertyUsageFlags.Storage) }
            }
        };
    }

    /// <summary>
    /// Checks if a collider's Identity matches any of this mapping's categories.
    /// Uses string-based CategoryName comparison for consistency with Identity.HasCategory.
    /// </summary>
    public bool Matches(Identity? identity)
    {
        if (identity == null || MatchCategories == null || MatchCategories.Count == 0)
        {
            return false;
        }

        foreach (var category in MatchCategories)
        {
            if (identity.HasCategory(category))
            {
                return true;
            }
        }
        return false;
    }
}
