namespace Jmodot.Core.AI.BB;

using System;

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
    T? GetVar<T>(StringName key) where T : class;

    /// <summary>
    ///     Sets a reference type (class) variable in the blackboard.
    ///     This change is propagated down to any child blackboards.
    /// </summary>
    /// <typeparam name="T">The type of the variable. Must be a class.</typeparam>
    /// <param name="key">The StringName key for the variable.</param>
    /// <param name="value">The value to set.</param>
    /// <returns>Error.Ok on success, or Error.InvalidData if the type is mismatched with an existing key.</returns>
    Error SetVar<T>(StringName key, T value) where T : class;

    /// <summary>
    ///     Retrieves a value type (struct) variable from the blackboard.
    ///     If the key is not found, it recursively checks the parent blackboard.
    /// </summary>
    /// <typeparam name="T">The expected type of the variable. Must be a struct.</typeparam>
    /// <param name="key">The StringName key of the variable to retrieve.</param>
    /// <returns>A nullable struct of type T. HasValue will be false if the key is not found or the type does not match.</returns>
    T? GetPrimVar<[MustBeVariant] T>(StringName key) where T : struct;

    /// <summary>
    ///     Sets a value type (struct) variable in the blackboard.
    ///     This change is propagated down to any child blackboards.
    /// </summary>
    /// <typeparam name="T">The type of the variable. Must be a struct.</typeparam>
    /// <param name="key">The StringName key for the variable.</param>
    /// <param name="val">The value to set.</param>
    /// <returns>Error.Ok on success, or Error.InvalidData if the type is mismatched with an existing key.</returns>
    Error SetPrimVar<[MustBeVariant] T>(StringName key, T val) where T : struct;

    /// <summary>
    /// Subscribes a callback action to be invoked whenever the value associated with the specified key changes.
    /// </summary>
    /// <param name="key">The StringName key to monitor.</param>
    /// <param name="callback">The action to execute when the value changes. The new value is passed as a Variant.</param>
    void Subscribe(StringName key, Action<Variant> callback);

    /// <summary>
    /// Unsubscribes a callback action from the specified key.
    /// </summary>
    /// <param name="key">The StringName key from which to unsubscribe.</param>
    /// <param name="callback">The specific action to remove.</param>
    void Unsubscribe(StringName key, Action<Variant> callback);
}
