namespace Jmodot.Core.Stats;

using global::Jmodot.Core.Stats;
using Godot.Collections;

/// <summary>
/// A universal interface for any environmental object or area that can temporarily
/// modify an actor's stats. This is used for effects like slippery ice,
/// sticky mud, or low-gravity zones. It provides a dictionary of modifiers
/// that a StatContextReceiver2D can apply to a character's IStatProvider.
/// </summary>
public interface IStatContextProvider
{
    /// <summary>
    /// A dictionary containing the modifiers this provider wishes to apply.
    /// The key is the Attribute to target (e.g., "Friction.tres"), and the value is a Resource
    /// representing an IModifier (e.g., a FloatAttributeModifier resource like "MultiplyBy_0.1.tres").
    /// </summary>
    Dictionary<Attribute, Resource> Modifiers { get; }
}
