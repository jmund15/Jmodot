namespace Jmodot.Core.Input;

/// <summary>
/// Describes the semantic meaning of a <see cref="Godot.Vector2"/> emitted by a
/// <see cref="VectorBindingBase"/>. Consumers that need to distinguish
/// "direction with deflection pressure" from "target position in world units"
/// MUST branch on this tag rather than on numeric magnitude. Direction-only
/// consumers can ignore the tag and call <c>.Normalized()</c>.
/// </summary>
public enum VectorInputSemantic
{
    /// <summary>
    /// Stick/WASD-style: length is in [0, 1] and represents deflection pressure;
    /// direction is given by the unit vector. Zero vector means "at rest / no input."
    /// </summary>
    Directional,

    /// <summary>
    /// Mouse-cursor-style: length is an offset from the entity in world units
    /// (meters); direction points toward the cursor. Zero only if the cursor
    /// is exactly on the entity (rare edge case).
    /// </summary>
    PositionalOffset
}
