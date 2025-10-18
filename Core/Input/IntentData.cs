namespace Jmodot.Core.Input;

using System;
using Jmodot.Implementation.Shared;

/// <summary>
///     A lightweight, type-safe container for a single piece of input intent data.
///     It acts as a "Tagged Union", holding one of several possible data types.
///     This revised design removes the explicit enum, relying on the safer and cleaner
///     "TryGet" pattern for value access.
/// </summary>
public readonly struct IntentData
{
    // Internal fields to hold the data. Using a "private readonly" field is a C# pattern
    // that allows us to differentiate which value is active, even without an enum.
    private readonly object _value;

    // --- Constructors ---
    public IntentData(bool value)
    {
        this._value = value;
    }

    public IntentData(float value)
    {
        this._value = value;
    }

    public IntentData(Vector2 value)
    {
        this._value = value;
    }


    // --- Safe "TryGet" Accessors ---
    public bool GetBool()
    {
        if (!TryGetBool(out var b))
        {
            throw JmoLogger.LogAndRethrow(
                new InvalidOperationException($"Cannot get bool from intent data  '{_value}'"), this);
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
        if (this._value is bool boolValue)
        {
            value = boolValue;
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
                new InvalidOperationException($"Cannot get float from intent data  '{_value}'"), this);
        }
        return f!.Value;
    }
    /// <summary>
    ///     Safely retrieves the value if it is a float.
    /// </summary>
    public bool TryGetFloat(out float? value)
    {
        if (this._value is float floatValue)
        {
            value = floatValue;
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
                new InvalidOperationException($"Cannot get Vector2 from intent data  '{_value}'"), this);
        }
        return vec2!.Value;
    }
    /// <summary>
    ///     Safely retrieves the value if it is a Vector2.
    /// </summary>
    public bool TryGetVector2(out Vector2? value)
    {
        if (this._value is Vector2 vector2Value)
        {
            value = vector2Value;
            return true;
        }

        value = null;
        return false;
    }

    public object GetValue()
    {
        return _value;
    }
}
