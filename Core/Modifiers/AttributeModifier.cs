namespace Jmodot.Core.Modifiers;

using Godot.Collections;

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
}
