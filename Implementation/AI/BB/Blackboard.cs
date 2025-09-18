namespace Jmodot.Implementation.AI.BB;

using System;
using System.Diagnostics;
using Core.AI.BB;
using Shared;
using System.Collections.Generic;

/// <summary>
///     A centralized data container for sharing state between various game systems, particularly for AI agents.
///     It uses StringName keys for performant access and supports a hierarchical structure for data inheritance and
///     propagation.
/// </summary>
[GlobalClass]
[Tool]
public partial class Blackboard : Node, IBlackboard
{
    protected IBlackboard? ParentBB { get; set; }
    protected Dictionary<StringName, Variant> BBData { get; set; } = new();


    // May have to use Action<object> because the Godot C# source generator cannot handle a Godot-specific
    // struct (Variant) as a generic type argument for a standard C# delegate (Action<T>).
    // The Variant is "boxed" as an object when the event is invoked.
    // This field is not serialized, so either Godot.Collections or System.Collections.Generic is fine.
    // We use Godot.Collections for consistency within the class.
    protected Dictionary<StringName, Action<Variant>> Subscriptions { get; set; } = new();
    /// <summary>
    ///     Establishes a parent blackboard, allowing this blackboard to fall back to the parent
    ///     for data retrieval if a key is not found locally.
    /// </summary>
    /// <param name="parentBB">The IBlackboard instance to use as a parent.</param>
    public void SetParent(IBlackboard? parentBB)
    {
        this.ParentBB = parentBB;
    }

    /// <summary>
    ///     Retrieves a reference type value from the blackboard.
    ///     Performs a recursive lookup through parent blackboards if the key is not found locally.
    /// </summary>
    /// <typeparam name="T">The expected object type, must be a class.</typeparam>
    /// <param name="key">The StringName identifier for the data.</param>
    /// <returns>The requested object, or null if it does not exist or has a mismatched type.</returns>
    public T? GetVar<T>(StringName key) where T : class
    {
        if (this.BBData.TryGetValue(key, out var val))
        {
            if (val.Obj is T tVal)
            {
                return tVal;
            }

            JmoLogger.Error(this, $"Requested data for key '{key}' exists, but the requested type '{typeof(T).Name}' does not match the stored type '{val.Obj?.GetType().Name}'.");
            return null;
        }

        if (this.ParentBB != null) { return this.ParentBB.GetVar<T>(key); }

        JmoLogger.Warning(this, $"Requested data with key '{key}' does not exist in this or any parent blackboard.");
        return null;
    }

    /// <summary>
    ///     Sets a reference type value in the blackboard. The new value must match the type of an existing value if present.
    ///     The value is also propagated down to any child blackboard.
    /// </summary>
    /// <typeparam name="T">The type of the object, must be a class.</typeparam>
    /// <param name="key">The StringName identifier for the data.</param>
    /// <param name="val">The object to store.</param>
    /// <returns>Error.Ok on success, Error.InvalidData if a type mismatch occurs.</returns>
    public Error SetVar<T>(StringName key, T val) where T : class
    {
        BBData.TryGetValue(key, out var oldVal);

        if (oldVal.Obj != null && val != null && oldVal.Obj.GetType() != val.GetType())
        {
            JmoLogger.Error(this, $"Inconsistent data type for key '{key}'. Original was '{oldVal.Obj.GetType().Name}', but attempted to set '{typeof(T).Name}'.");
            return Error.InvalidData;
        }

        if (Equals(oldVal.Obj, val))
        {
            return Error.Ok; // No change, no event
        }

        this.BBData[key] = Variant.From(val);
        NotifySubscribers(key, Variant.From(val));
        return Error.Ok;
    }

    /// <summary>
    ///     Retrieves a value type (struct) from the blackboard.
    ///     Performs a recursive lookup through parent blackboards if the key is not found locally.
    /// </summary>
    /// <typeparam name="T">The expected struct type.</typeparam>
    /// <param name="key">The StringName identifier for the data.</param>
    /// <returns>A nullable struct. Returns the value if found, or null if it does not exist or has a mismatched type.</returns>
    public T? GetPrimVar<[MustBeVariant] T>(StringName key) where T : struct
    {
        if (this.BBData.TryGetValue(key, out var val))
        {
            if (val.Obj is T tVal)
            {
                return tVal;
            }
            JmoLogger.Error(this, $"Requested primitive data for key '{key}' exists, but the requested type '{typeof(T).Name}' does not match the stored type '{val.GetType()}'.");
            return null;
        }

        if (this.ParentBB != null)
        {
            return this.ParentBB.GetPrimVar<T>(key);
        }

        JmoLogger.Warning(this, $"Requested primitive data with key '{key}' does not exist in this or any parent blackboard.");
        return null;
    }

    /// <summary>
    ///     Sets a value type (struct) in the blackboard. The new value must match the type of an existing value if present.
    ///     The value is also propagated down to any child blackboard.
    /// </summary>
    /// <typeparam name="T">The type of the struct.</typeparam>
    /// <param name="key">The StringName identifier for the data.</param>
    /// <param name="val">The struct to store.</param>
    /// <returns>Error.Ok on success, Error.InvalidData if a type mismatch occurs.</returns>
    public Error SetPrimVar<[MustBeVariant] T>(StringName key, T val) where T : struct
    {
        BBData.TryGetValue(key, out var oldVal);

        if (oldVal.VariantType != Variant.Type.Nil && oldVal.Obj is not T)
        {
            JmoLogger.Error(this, $"Inconsistent primitive data type for key '{key}'. Original was '{oldVal.GetType()}', but attempted to set '{typeof(T).Name}'.");
            return Error.InvalidData;
        }

        if (oldVal.VariantType != Variant.Type.Nil && oldVal.Obj is T tVal &&
            tVal.Equals(val))
        {
            return Error.Ok; // No change, no event
        }

        this.BBData[key] = Variant.From(val);
        NotifySubscribers(key, Variant.From(val));
        return Error.Ok;
    }
    public void Subscribe(StringName key, Action<Variant> callback)
    {
        if (Subscriptions.ContainsKey(key))
        {
            Subscriptions[key] += callback;
        }
        else
        {
            Subscriptions[key] = callback;
        }
    }

    public void Unsubscribe(StringName key, Action<Variant> callback)
    {
        if (Subscriptions.ContainsKey(key))
        {
            Subscriptions[key] -= callback;
            if (Subscriptions[key] == null)
            {
                Subscriptions.Remove(key);
            }
        }
    }

    private void NotifySubscribers(StringName key, Variant newValue)
    {
        if (Subscriptions.TryGetValue(key, out var callbacks))
        {
            callbacks?.Invoke(newValue);
        }
    }
}
