namespace Jmodot.Core.Modifiers;

using System;

/// <summary>
/// The internal, non-generic contract for a ModifiableProperty.
/// This is used by the StatController to interact with its collection of stat
/// properties in a type-agnostic way. It should not be used by external systems.
/// </summary>
public interface IModifiableProperty
{
    /// <summary>
    /// Gets the final, calculated value of the property, boxed into a Variant.
    /// </summary>
    Variant GetValueAsVariant();

    /// <summary>
    /// Fired when the calculated value of this property changes.
    /// The payload is the new value boxed in a Variant.
    /// </summary>
    event Action<Variant> OnValueChanged;

    /// <summary>
    /// Adds a modifier from a generic Resource and a given owner.
    /// </summary>
    /// <returns>A unique Guid for this specific modifier application, or Guid.Empty on failure.</returns>
    Guid AddModifier(Resource modifierResource, object owner);

    /// <summary>
    /// Removes a single modifier application using its unique ID.
    /// </summary>
    void RemoveModifier(Guid modifierId);

    /// <summary>
    /// Removes all modifiers that were applied by a specific owner.
    /// </summary>
    void RemoveAllModifiersFromSource(object owner);
}
