#region

using Jmodot.Core.Shared;

#endregion

namespace Jmodot.Core.Movement;

/// <summary>
///     The definitive, low-level interface for directly manipulating a Godot 3D physics body.
///     It provides a set of simple, direct, and unambiguous commands to control a body's
///     velocity and execute the final physics step. This is a pure "driver" that contains no
///     gameplay logic or complex state buffering, acting as a standardized adapter for any
///     3D physics node.
/// </summary>
public interface ICharacterController3D : IGodotNodeInterface
{
    // --- Core State (Read-Only) ---

    /// <summary>Gets the global position of the physics body in world space.</summary>
    Vector3 GlobalPosition { get; }

    /// <summary>Gets the current linear velocity of the physics body.</summary>
    Vector3 Velocity { get; }

    /// <summary>Gets whether the physics body is currently considered to be on the floor.</summary>
    bool IsOnFloor { get; }

    // --- Core Commands ---

    /// <summary>
    ///     Directly sets the body's entire velocity vector. This is an absolute override,
    ///     discarding any previous velocity from the current frame's calculation. This command
    ///     takes effect during the next `Move()` call.
    /// </summary>
    /// <param name="newVelocity">The exact velocity vector the body should have.</param>
    void SetVelocity(Vector3 newVelocity);

    /// <summary>
    ///     Adds a velocity vector to the body's current velocity. This is the fundamental
    ///     method for applying all momentary forces, such as impulses, gravity, and external
    ///     environmental effects like knockback or wind.
    /// </summary>
    /// <param name="additiveVelocity">The velocity vector to add.</param>
    void AddVelocity(Vector3 additiveVelocity);

    /// <summary>
    ///     Executes the final movement for the physics frame using the body's
    ///     current, modified velocity and Godot's internal physics simulation (e.g., MoveAndSlide).
    ///     This method should be called once at the end of all velocity calculations for a frame.
    /// </summary>
    void Move();

    /// <summary>
    ///     Bypasses all physics simulation for one frame and immediately moves the body to the
    ///     specified global position. This is the correct method for spawning, checkpointing,
    ///     or snapping a character to a position.
    /// </summary>
    /// <param name="newGlobalPosition">The target world-space position.</param>
    void Teleport(Vector3 newGlobalPosition);
}