namespace Jmodot.Core.Input;

using System;
using Jmodot.Implementation.Shared;

/// <summary>
///     A lightweight, type-safe container for a single piece of input intent data.
///     Implemented as a typed discriminated union with separate fields per type,
///     eliminating boxing allocations on the hot path (constructed every frame).
/// </summary>
public readonly struct IntentData
{
    private enum ValueType : byte { None = 0, Bool = 1, Float = 2, Vector2 = 3 }

    private readonly ValueType _type;
    private readonly bool _boolValue;
    private readonly float _floatValue;
    private readonly Vector2 _vec2Value;

    // --- Constructors ---
    public IntentData(bool value)
    {
        _type = ValueType.Bool;
        _boolValue = value;
    }

    public IntentData(float value)
    {
        _type = ValueType.Float;
        _floatValue = value;
    }

    public IntentData(Vector2 value)
    {
        _type = ValueType.Vector2;
        _vec2Value = value;
    }

    // --- Safe "TryGet" Accessors ---
    public bool GetBool()
    {
        if (!TryGetBool(out var b))
        {
            throw JmoLogger.LogAndRethrow(
                new InvalidOperationException($"Cannot get bool from IntentData of type '{_type}'"), this);
        }
        return b!.Value;
    }

    /// <summary>
    ///     Safely retrieves the value if it is a bool. This is the primary and safest
    ///     way to access the contained data.
    /// </summary>
    /// <param name="value">The retrieved boolean value, if successful.</param>
    /// <returns>True if the held data was a bool, otherwise false.</returns>
    public bool TryGetBool(out bool? value)
    {
        if (_type == ValueType.Bool)
        {
            value = _boolValue;
            return true;
        }

        value = null;
        return false;
    }

    public float GetFloat()
    {
        if (!TryGetFloat(out var f))
        {
            throw JmoLogger.LogAndRethrow(
                new InvalidOperationException($"Cannot get float from IntentData of type '{_type}'"), this);
        }
        return f!.Value;
    }

    /// <summary>
    ///     Safely retrieves the value if it is a float.
    /// </summary>
    public bool TryGetFloat(out float? value)
    {
        if (_type == ValueType.Float)
        {
            value = _floatValue;
            return true;
        }

        value = null;
        return false;
    }

    public Vector2 GetVector2()
    {
        if (!TryGetVector2(out var vec2))
        {
            throw JmoLogger.LogAndRethrow(
                new InvalidOperationException($"Cannot get Vector2 from IntentData of type '{_type}'"), this);
        }
        return vec2!.Value;
    }

    /// <summary>
    ///     Safely retrieves the value if it is a Vector2.
    /// </summary>
    public bool TryGetVector2(out Vector2? value)
    {
        if (_type == ValueType.Vector2)
        {
            value = _vec2Value;
            return true;
        }

        value = null;
        return false;
    }

    /// <summary>
    ///     Returns the value as a boxed object. This is the backward-compatible path
    ///     that does allocate — use typed accessors (GetBool/GetFloat/GetVector2) on
    ///     the hot path instead.
    /// </summary>
    public object GetValue()
    {
        return _type switch
        {
            ValueType.Bool => _boolValue,
            ValueType.Float => _floatValue,
            ValueType.Vector2 => _vec2Value,
            _ => throw JmoLogger.LogAndRethrow(
                new InvalidOperationException("IntentData has no value (default struct)."), this)
        };
    }
}
