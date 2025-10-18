namespace Jmodot.Core.AI.BB;

using System;

// TODO: make try-get and get functions
//  try-get are nullable return options
//  get throw errors if not found

/// <summary>
///     Defines the contract for a Blackboard, a centralized data store for AI and game systems.
///     This allows for decoupled communication by enabling different components to read and write
///     data to a shared blackboard without needing direct references to each other.
/// </summary>
public interface IBlackboard
{
    /// <summary>
    ///     Sets a parent blackboard, creating a hierarchy for data lookups.
    ///     If a variable is not found in this blackboard, the request is forwarded to the parent.
    /// </summary>
    /// <param name="parent">The blackboard to set as the parent.</param>
    void SetParent(IBlackboard parent);

    /// <summary>
    ///     Retrieves a reference type (class) variable from the blackboard.
    ///     If the key is not found, it recursively checks the parent blackboard.
    /// </summary>
    /// <typeparam name="T">The expected type of the variable. Must be a class.</typeparam>
    /// <param name="key">The StringName key of the variable to retrieve.</param>
    /// <returns>The variable as type T, or null if not found or if the type does not match.</returns>
    T Get<T>(StringName key);
    /// <summary>
    /// Tries to get a value. Returns true and provides the value if successful.
    /// </summary>
    bool TryGet<T>(StringName key, out T? value);

    /// <summary>
    ///     Sets a reference type (class) variable in the blackboard.
    ///     This change is propagated down to any child blackboards.
    /// </summary>
    /// <typeparam name="T">The type of the variable. Must be a class.</typeparam>
    /// <param name="key">The StringName key for the variable.</param>
    /// <param name="value">The value to set.</param>
    /// <returns>Error.Ok on success, or Error.InvalidData if the type is mismatched with an existing key.</returns>
    Error Set<T>(StringName key, T value);

    /// <summary>
    /// Subscribes a callback action to be invoked whenever the value associated with the specified key changes.
    /// </summary>
    /// <param name="key">The StringName key to monitor.</param>
    /// <param name="callback">The action to execute when the value changes. The new value is passed as a Variant.</param>
    void Subscribe(StringName key, Action<object> callback);

    /// <summary>
    /// Unsubscribes a callback action from the specified key.
    /// </summary>
    /// <param name="key">The StringName key from which to unsubscribe.</param>
    /// <param name="callback">The specific action to remove.</param>
    void Unsubscribe(StringName key, Action<object> callback);
}
