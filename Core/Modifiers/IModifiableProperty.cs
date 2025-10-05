namespace Jmodot.Core.Modifiers;

/// <summary>
/// A non-generic interface for ModifiableProperty<T>.
/// This allows different types of modifiable properties (float, bool, etc.)
/// to be stored in a single collection, with their final value retrieved as a Variant.
/// </summary>
public interface IModifiableProperty
{
    /// <summary>
    /// Gets the final, calculated value of the property, boxed into a Variant.
    /// </summary>
    Variant GetValueAsVariant();

    /// <summary>
    /// A non-generic gatekeeper method to add a modifier from a Resource.
    /// The implementation is responsible for validating and casting the resource
    /// to the correct, strongly-typed IModifier<T>.
    /// </summary>
    /// <param name="modifierResource">The modifier resource to add.</param>
    /// <returns>True if the modifier was of the correct type and was added, false otherwise.</returns>
    bool TryAddModifier(Resource modifierResource);
}
