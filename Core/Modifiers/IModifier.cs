#region

using Godot.Collections;

#endregion

namespace Jmodot.Core.Modifiers;

/// <summary>
///     The generic interface for any object that can modify a value. It contains all the
///     necessary contracts for priority, calculation stage, and tag-based conflict resolution.
/// </summary>
/// <typeparam name="T">The type of value to be modified.</typeparam>
public interface IModifier<T>
{
    /// <summary>The priority of this modifier within its stage. Higher numeric values are applied first.</summary>
    int Priority { get; }

    /// <summary>A list of simple string tags that this modifier possesses (e.g., "Slippery", "Fire").</summary>
    Array<string> Tags { get; }

    /// <summary>A list of tags that this modifier will cancel out from the calculation pipeline.</summary>
    Array<string> CancelsTags { get; }

    /// <summary>
    ///     Applies the modification to a given value.
    /// </summary>
    /// <param name="currentValue">The value as calculated from all previous stages and modifiers.</param>
    /// <returns>The newly modified value.</returns>
    T Modify(T currentValue);
}