namespace Jmodot.Implementation.Shared;

using Jmodot.Core.Movement;

public static class MovementExtensions
{
    #region VECTOR_EXTENSIONS
    /// <summary>
    /// Gets a Vector2 from a Vector3, ignoring the Y component.
    /// </summary>
    public static Vector2 GetFlattenedVector2(this Vector3 vec) => new(vec.X, vec.Z);
    /// <summary>
    /// Gets a Vector3 from a Vector2, inserting 0 for the Y component: (X, 0, Y).
    /// </summary>
    public static Vector3 GetFlatVector3(this Vector2 vec) => new(vec.X, 0f, vec.Y);
    #endregion
    #region VELOCITY_EXTENSIONS

    public const float DEFAULT_WEIGHT_PERCENTAGE = 0.075f;

    public static Vector3 GetWeightedGravity3D(this CharacterBody3D body,
        float weightPercentage = DEFAULT_WEIGHT_PERCENTAGE)
    {
        Vector3 weightedGrav;
        if (body.Velocity.Y < 0)
        {
            weightedGrav = body.GetGravity() - body.Velocity * body.GetGravity() * weightPercentage;
        }
        else
        {
            weightedGrav = body.GetGravity();
        }

        return weightedGrav;
    }

    public static Vector3 GetCustomWeightedGravity(this CharacterBody3D body,
        Vector3 customGravity, float weightPercentage)
    {
        Vector3 weightedGrav;
        if (body.Velocity.Y < 0)
        {
            weightedGrav = customGravity - body.Velocity * customGravity * weightPercentage;
        }
        else
        {
            weightedGrav = customGravity;
        }

        return weightedGrav;
    }

    /// <summary>
    /// Resolves the linear velocity of a Node3D collider via the most appropriate
    /// interface, in priority order: RigidBody3D.LinearVelocity → CharacterBody3D.Velocity
    /// → IVelocityProvider3D.LinearVelocity → Vector3.Zero (static / unknown).
    /// Useful for collision response code that needs the kicker's velocity without
    /// caring about the body type.
    /// </summary>
    public static Vector3 ResolveLinearVelocity(this Node3D collider)
    {
        if (collider is RigidBody3D rb) { return rb.LinearVelocity; }
        if (collider is CharacterBody3D cb) { return cb.Velocity; }
        if (collider is IVelocityProvider3D vp) { return vp.LinearVelocity; }
        return Vector3.Zero;
    }

    #endregion
}
