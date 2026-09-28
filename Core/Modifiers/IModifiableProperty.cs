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
    /// Implementations will likely copy over modifiers shallowly
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
    /// Adds an authored modifier on behalf of <paramref name="owner"/>.
    /// </summary>
    /// <returns>A unique Guid for this application, or <see cref="Guid.Empty"/> when <paramref name="modifier"/> is null.</returns>
    /// <exception cref="InvalidCastException">The modifier's value type does not match this property's.</exception>
    Guid AddModifier(AttributeModifier? modifier, object owner);

    /// <summary>
    /// Removes a single modifier application using its unique ID.
    /// </summary>
    void RemoveModifier(Guid modifierId);

    /// <summary>
    /// Removes all modifiers that were applied by a specific owner.
    /// </summary>
    void RemoveAllModifiersFromSource(object owner);

    /// <summary>
    /// Adds every active modifier of this property, with its original owner, to <paramref name="target"/>.
    /// Used for merging stat sheets (e.g. Blueprint -> Instance). No modifier is dropped: either all transfer
    /// or the call throws before <paramref name="target"/> changes.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// <paramref name="target"/> holds another value type and a modifier here is not an <see cref="AttributeModifier"/>.
    /// </exception>
    /// <exception cref="InvalidCastException">
    /// <paramref name="target"/> holds another value type that the modifiers here cannot apply to.
    /// </exception>
    void TransferModifiersTo(IModifiableProperty target);

    /// <summary>
    /// Sets the base value of the property without removing any active modifiers.
    /// Modifiers will be recalculated on top of the new base value.
    /// </summary>
    /// <param name="newBaseValue">The new base value, boxed in a Variant.</param>
    void SetBaseValue(Variant newBaseValue);
}
