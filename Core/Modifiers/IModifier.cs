namespace Jmodot.Core.Modifiers;

using Godot.Collections;
using Stats;

/// <summary>
///     The generic interface for any object that can modify a value. It contains all the
///     necessary contracts for priority, calculation stage, and tag-based conflict resolution.
/// </summary>
/// <typeparam name="T">The type of value to be modified.</typeparam>
public interface IModifier<T>
{
    /// <summary>The priority of this modifier within its stage. Higher numeric values are applied first.</summary>
    int Priority { get; }

    // TODO: look into optimizing tags with hashsets!
    //      probably have hashsets as based, and get functions override to turn the exported arrays from resource mods into hashsets

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

    /// <summary>
    ///     Applies the modification to a given value.
    /// </summary>
    /// <param name="currentValue">The value as calculated from all previous stages and modifiers.</param>
    /// <returns>The newly modified value.</returns>
    T Modify(T currentValue);
}
