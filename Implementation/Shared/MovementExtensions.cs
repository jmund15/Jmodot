namespace Jmodot.Implementation.Shared;

public static class MovementExtensions
{
    #region VECTOR_EXTENSIONS
    /// <summary>
    /// Gets a Vector2 from a Vector3, ignoring the Y component.
    /// </summary>
    public static Vector2 GetFlattenedVector2(this Vector3 vec) => new(vec.X, vec.Z);
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

        //GD.Print("weighted grav: ", weightedGrav);
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

        //GD.Print("weighted grav: ", weightedGrav);
        return weightedGrav;
    }

    #endregion
}
