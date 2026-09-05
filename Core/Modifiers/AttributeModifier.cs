namespace Jmodot.Core.Modifiers;

using Godot.Collections;
using Jmodot.Core.Stats;
using Jmodot.Implementation.Shared;
using System;
using StatAttribute = Jmodot.Core.Stats.Attribute;

/// <summary>
///     Authoring-side base for every modifier a StatController can hold. Exists so an [Export] can be typed
///     against the family (Godot's New menu then lists only the concrete modifiers); the behavioral contract
///     stays on <see cref="IModifier{T}" /> and <see cref="ITaggableModifier" />. Carries the members every
///     concrete modifier shares: <see cref="Priority" /> and the cancel/context tag arrays.
/// </summary>
[GlobalClass, Tool]
public abstract partial class AttributeModifier : Resource, ITaggableModifier
{
    [Export] public int Priority { get; protected set; }

    [ExportGroup("EffectTags & Cancellation")]
    [Export] public Array<string> EffectTags { get; protected set; } = new();
    [Export] public Array<string> CancelsEffectTags { get; protected set; } = new();
    [Export] public Array<string> ContextTags { get; protected set; } = new();
    [Export] public Array<string> RequiredContextTags { get; protected set; } = new();

    /// <summary>
    /// Narrows an authored untyped resource to an attribute modifier. Returns null and warns when the resource is null; throws when it is another resource type.
    /// </summary>
    public static AttributeModifier? FromUntyped(Resource? resource, StatAttribute attribute, object context)
    {
        if (resource is null)
        {
            JmoLogger.Warning(context,
                $"Cannot narrow modifier for '{attribute.AttributeName}': incoming resource is null. Check for stripped sub_resources in any .tres that exports this attribute.");
            return null;
        }

        if (resource is AttributeModifier modifier)
        {
            return modifier;
        }

        throw JmoLogger.LogAndRethrow(new InvalidCastException(
                $"Resource of type {resource.GetType().Name} ('{resource.ResourceName}') is not an {nameof(AttributeModifier)} and cannot be applied to '{attribute.AttributeName}'."),
            context);
    }
}
