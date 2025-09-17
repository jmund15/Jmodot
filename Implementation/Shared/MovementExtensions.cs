namespace Jmodot.Implementation.Shared;

public static class MovementExtensions
{
    #region VELOCITY_EXTENSIONS

    public const float DEFAULT_WEIGHT_PERCENTAGE = 0.075f;

    public static Vector3 GetWeightedGravity(this CharacterBody3D body,
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
