namespace Jmodot.Core.Modifiers;

using Godot.Collections;

/// <summary>
///     Opt-in tag capability for modifiers that participate in the cancel/context-gate filtering
///     performed by <c>ModifiableProperty.GetFinalModifiers</c>. Modifiers that never flow through
///     that filter (e.g. the currency award pipeline, which folds via <c>Calculate</c> directly) do
///     not implement this — they carry no tags and are never cancelled or context-gated.
/// </summary>
public interface ITaggableModifier
{
    /// <summary>A list of simple string effect tags that this modifier possesses (e.g., "Slippery", "Damage Boost").</summary>
    Array<string> EffectTags { get; }

    /// <summary>A list of tags that this modifier will cancel out from the calculation pipeline.</summary>
    Array<string> CancelsEffectTags { get; }

    /// <summary>
    /// A list of tags defining what context of a modifier this is.
    /// e.g. Ice, Poison, Gravity, Power up, etc.
    /// </summary>
    Array<string> ContextTags { get; }

    /// <summary>
    /// A list of tags that are required for this modifier to be active.
    /// If this list is null or empty, the modifier is always active.
    /// If the list has entries, the modifier will only be included in the calculation
    /// if the character has at least one of these contexts active.
    /// </summary>
    Array<string> RequiredContextTags { get; }
}
