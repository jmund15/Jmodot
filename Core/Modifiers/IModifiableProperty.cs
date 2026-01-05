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
    /// Creates a Copy of the Property.
    /// Implementations will likely copy oover modifiers shallowly
    /// (i.e. not creating direct copies of the GUIDs of the mods or the mods themselves)
    /// </summary>
    /// <returns></returns>
    IModifiableProperty Clone();

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

    /// <summary>
    /// Transfers all active modifiers from this property to a target property.
    /// This is used for merging stat sheets (e.g. Blueprint -> Instance).
    /// </summary>
    void TransferModifiersTo(IModifiableProperty target);

    /// <summary>
    /// Sets the base value of the property without removing any active modifiers.
    /// Modifiers will be recalculated on top of the new base value.
    /// </summary>
    /// <param name="newBaseValue">The new base value, boxed in a Variant.</param>
    void SetBaseValue(Variant newBaseValue);
}
