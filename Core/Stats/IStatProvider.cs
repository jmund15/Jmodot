namespace Jmodot.Core.Stats;

using System;
using Implementation.Modifiers;
using Mechanics;
using Modifiers;

/// <summary>
///     Defines the public contract for a component that acts as the source of truth
///     for an entity's dynamic properties (stats). It provides a standardized way
///     to query calculated stat values.
/// </summary>
public interface IStatProvider
{
    /// <summary>
    ///     Emitted whenever the final calculated value of a stat changes.
    /// </summary>
    event Action<Attribute, Variant> OnStatChanged;

    /// <summary>
    ///     Retrieves the underlying ModifiableProperty object for systems that need to add/remove modifiers.
    /// </summary>
    ModifiableProperty<T> GetStat<T>(Attribute attribute);
    /// <summary>
    ///     Retrieves the final, calculated VALUE of a stat in a type-safe manner.
    ///     This is the primary method for any system that consumes stat data.
    /// </summary>
    T GetStatValue<[MustBeVariant] T>(Attribute attribute, T defaultValue = default(T));
    /// <summary>
    ///     Retrieves the final, calculated data for a specific mechanic.
    /// </summary>
    /// <param name="mechanicType">The mechanic to retrieve data for.</param>
    /// <returns>The final MechanicData object, or null if the entity cannot perform this mechanic.</returns>
    T GetMechanicData<T>(MechanicType mechanicType) where T : MechanicData;
    // TryGetMechanicData

    /// <summary>
    /// The "scalpel": Applies a modifier and returns a handle for precise removal later.
    /// Use this return handle for buffs/debuffs that may have multiple stacks or need to be removed individually.
    /// </summary>
    /// <param name="attribute">The stat to modify.</param>
    /// <param name="modifierResource">The modifier data to apply.</param>
    /// <param name="owner">The object source of this modification (e.g., the buff instance, an item).</param>
    /// <param name="handle">A unique ModifierHandle for this application, or null on failure.</param>
    bool TryAddModifier(Attribute attribute, Resource modifierResource, object owner, out ModifierHandle? handle);
    /// <summary>
    /// The "scalpel": Removes a single, specific modifier application using its handle.
    /// </summary>
    void RemoveModifier(ModifierHandle handle);
    /// <summary>
    /// The "sledgehammer": Removes ALL modifiers from a specific owner across ALL stats.
    /// Use this for declarative cleanup when a state ends, an item is unequipped, or a buff expires.
    /// </summary>
    void RemoveAllModifiersFromSource(object owner);
    /// <summary>
    /// A convenience method for applying a pre-defined set of modifiers associated with a character state.
    /// </summary>
    void AddActiveContext(StatContext context);
    /// <summary>
    /// A convenience method for cleaning up all modifiers applied by a character state.
    /// </summary>
    void RemoveActiveContext(StatContext context);
}
