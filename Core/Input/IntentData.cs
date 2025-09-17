namespace Jmodot.Core.Input;

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

    /// <summary>
    ///     Safely retrieves the value if it is a bool. This is the primary and safest
    ///     way to access the contained data.
    /// </summary>
    /// <param name="value">The retrieved boolean value, if successful.</param>
    /// <returns>True if the held data was a bool, otherwise false.</returns>
    public bool TryGetBool(out bool value)
    {
        if (this._value is bool boolValue)
        {
            value = boolValue;
            return true;
        }

        value = default;
        return false;
    }

    /// <summary>
    ///     Safely retrieves the value if it is a float.
    /// </summary>
    public bool TryGetFloat(out float value)
    {
        if (this._value is float floatValue)
        {
            value = floatValue;
            return true;
        }

        value = default;
        return false;
    }

    /// <summary>
    ///     Safely retrieves the value if it is a Vector2.
    /// </summary>
    public bool TryGetVector2(out Vector2 value)
    {
        if (this._value is Vector2 vector2Value)
        {
            value = vector2Value;
            return true;
        }

        value = default;
        return false;
    }
}
