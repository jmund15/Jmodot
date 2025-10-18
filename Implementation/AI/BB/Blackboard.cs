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
    //protected Dictionary<StringName, Variant> BBData { get; set; } = new();
    // For Godot-compatible types (GodotObject, primitives, engine structs)
    private Dictionary<StringName, Variant> _variantData = new();

    // For pure C# objects (POCOs)
    private Dictionary<StringName, object> _pocoData = new();


    // May have to use Action<object> because the Godot C# source generator cannot handle a Godot-specific
    // struct (Variant) as a generic type argument for a standard C# delegate (Action<T>).
    // The Variant is "boxed" as an object when the event is invoked.
    // This field is not serialized, so either Godot.Collections or System.Collections.Generic is fine.
    // We use Godot.Collections for consistency within the class.
    protected Dictionary<StringName, Action<object>> Subscriptions { get; set; } = new();
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
    /// <summary>
    /// Sets a value in the blackboard. It intelligently stores the value
    /// either as a Variant (for Godot types) or a direct object reference (for POCOs).
    /// </summary>
    public Error Set<T>(StringName key, T val)
    {
        // Null values can be tricky, let's handle them explicitly.
        // We'll store them in the variant dictionary as a Nil variant.
        if (val == null)
        {
            _pocoData.Remove(key);
            _variantData[key] = default; // Variant.Type.Nil
            NotifySubscribers(key, default);
            return Error.Ok;
        }

        // The key distinction: Does T inherit from GodotObject?
        if (val is GodotObject godotObj)
        {
            // This is a Godot type, use the Variant dictionary.
            _pocoData.Remove(key); // Ensure no key collision.
            if (_variantData.TryGetValue(key, out var oldVal) && oldVal.Obj == godotObj)
            {
                return Error.Ok; // No change
            }
            Variant v = Variant.From(godotObj);
            _variantData[key] = v;
            NotifySubscribers(key, v);
        }
        else if (typeof(T).IsValueType)
        {
             // This is a struct (int, float, Vector2, etc.).
             // Godot can convert most common structs to Variant.
            _pocoData.Remove(key);
            Variant v = Variant.From(val); // This will work for types with [MustBeVariant]
            if (_variantData.TryGetValue(key, out var oldVal) && oldVal.Equals(v))
            {
                 return Error.Ok; // No change
            }
            _variantData[key] = v;
            NotifySubscribers(key, v);
        }
        else
        {
            // This is a POCO (like your CharacterBodyController2D).
            // Use the object dictionary.
            _variantData.Remove(key); // Ensure no key collision.
            if (_pocoData.TryGetValue(key, out var oldVal) && ReferenceEquals(oldVal, val))
            {
                return Error.Ok; // No change
            }
            _pocoData[key] = val;
            // Note: POCOs can't be easily sent through the subscription system
            // unless you change its signature to Action<object>.
            // For now, we'll notify with a null variant.
            NotifySubscribers(key, default);
        }

        return Error.Ok;
    }

    /// <summary>
    /// Tries to retrieve a value from the blackboard. This method is safe and will not throw exceptions
    /// for missing keys or type mismatches.
    /// </summary>
    /// <param name="key">The StringName identifier for the data.</param>
    /// <param name="value">When this method returns, contains the requested value if the key was found
    /// and the type was correct; otherwise, the default value for the type T.</param>
    /// <returns>true if the key was found and the value was successfully cast; otherwise, false.</returns>
    public bool TryGet<T>(StringName key, out T? value)
    {
        // Initialize the out parameter
        value = default;

        // 1. Check for a POCO
        if (_pocoData.TryGetValue(key, out var pocoVal))
        {
            if (pocoVal is T typedPoco)
            {
                value = typedPoco;
                return true;
            }
            // Key exists, but type is wrong. This is a failure.
            JmoLogger.Error(this, $"TryGet failed for key '{key}': Stored POCO type '{pocoVal?.GetType().Name}' does not match requested type '{typeof(T).Name}'.");
            return false;
        }

        // 2. Check for a Variant
        if (_variantData.TryGetValue(key, out var variantVal))
        {
            if (variantVal.Obj is T typedValue)
            {
                value = typedValue;
                return true;
            }

            // Key exists, but type is wrong. This is a failure (unless it's just Nil).
            if (variantVal.VariantType != Variant.Type.Nil)
            {
                JmoLogger.Error(this, $"TryGet failed for key '{key}': Stored Variant type '{variantVal.Obj?.GetType().Name}' does not match requested type '{typeof(T).Name}'.");
            }
            return false;
        }

        // 3. Recurse to parent
        if (ParentBB != null)
        {
            return ParentBB.TryGet(key, out value);
        }

        // 4. Key not found anywhere
        return false;
    }
    /// <summary>
    /// Retrieves a value from the blackboard, assuming it exists. Use this method when the absence
    /// of the key or a type mismatch should be considered a critical, program-breaking error.
    /// </summary>
    /// <exception cref="KeyNotFoundException">Thrown if the key does not exist in this blackboard or any of its parents.</exception>
    /// <exception cref="InvalidCastException">Thrown if a value is found for the key, but it cannot be cast to the requested type T.</exception>
    public T Get<T>(StringName key)
    {
        if (TryGet<T>(key, out T? value))
        {
            // Null is a valid value, but if T is a non-nullable value type,
            // 'value' can't be null here. If T is a reference type, it can be.
            // We return it as is.
            return value!; // Using null-forgiving operator as TryGet succeeded.
        }

        // If TryGet failed, we need to find out why to throw the correct exception.
        // This adds a bit of redundant lookup, but it's necessary for good error reporting.
        if (_pocoData.ContainsKey(key) || _variantData.ContainsKey(key))
        {
            // The key exists, so the failure must have been a type mismatch.
            object? storedValue = _pocoData.TryGetValue(key, out var pVal) ? pVal : _variantData[key].Obj;
            throw new InvalidCastException($"The value for key '{key}' is of type '{storedValue?.GetType().Name}', which cannot be cast to '{typeof(T).Name}'.");
        }

        // If we are here, the key truly doesn't exist.
        throw new KeyNotFoundException($"The key '{key}' was not found in the blackboard.");
    }
    public void Subscribe(StringName key, Action<object> callback)
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

    public void Unsubscribe(StringName key, Action<object> callback)
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

    private void NotifySubscribers(StringName key, object newValue)
    {
        if (Subscriptions.TryGetValue(key, out var callbacks))
        {
            callbacks?.Invoke(newValue);
        }
    }
}
