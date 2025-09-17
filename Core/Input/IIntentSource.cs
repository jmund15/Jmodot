namespace Jmodot.Core.Input;

using System.Collections.Generic;

/// <summary>
///     Defines a contract for a component that provides player or AI intent.
///     Its single responsibility is to translate raw input into a standardized
///     and easily consumable collection of abstract InputActions and their values.
/// </summary>
public interface IIntentSource
{
    /// <summary>
    ///     Gets a read-only collection of all active intents for the current frame.
    ///     This is the primary method for any system to query what an entity wants to do.
    /// </summary>
    /// <returns>A read-only dictionary of InputAction keys and their corresponding IntentData values.</returns>
    IReadOnlyDictionary<InputAction, IntentData> GetIntents();
}
