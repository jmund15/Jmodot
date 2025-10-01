namespace Jmodot.Core.Stats;

using System;
using Mechanics;
using Modifiers;

/// <summary>
///     Defines the public contract for a component that acts as the source of truth
///     for an entity's dynamic properties (stats). It provides a standardized way
///     to query final, calculated stat values.
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
    ModifiableProperty<Variant> GetStat(Attribute attribute, MovementMode? context = null);

    /// <summary>
    ///     Retrieves the final, calculated VALUE of a stat in a type-safe manner.
    ///     This is the primary method for any system that consumes stat data.
    /// </summary>
    T GetStatValue<[MustBeVariant] T>(Attribute attribute, MovementMode? context = null, T defaultValue = default(T));

    /// <summary>
    ///     Retrieves the final, calculated data for a specific mechanic.
    /// </summary>
    /// <param name="mechanicType">The mechanic to retrieve data for.</param>
    /// <returns>The final MechanicData object, or null if the entity cannot perform this mechanic.</returns>
    T? GetMechanicData<T>(MechanicType mechanicType) where T : MechanicData;
}
