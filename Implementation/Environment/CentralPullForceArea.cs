namespace Jmodot.Implementation.Environment;

using Godot;
using Core.Environment;

/// <summary>
/// A force provider that pulls targets toward the area's center.
/// Used for escape resistance, gravity wells, and tractor beams.
/// Force = directionToCenter * pullStrength
/// </summary>
[GlobalClass]
public partial class CentralPullForceArea : Area3D, IForceProvider3D
{
    private float _pullStrength = 0f;

    /// <summary>
    /// Initialize the central pull force area.
    /// </summary>
    /// <param name="pullStrength">Flat force magnitude toward center.</param>
    public void Initialize(float pullStrength)
    {
        _pullStrength = pullStrength;
    }

    /// <summary>
    /// Returns the pull force toward the area's center.
    /// </summary>
    public Vector3 GetForceFor(Node3D target)
    {
        if (target == null || _pullStrength <= 0)
        {
            return Vector3.Zero;
        }

        var centerPos = GlobalPosition;
        var targetPos = target.GlobalPosition;
        var toCenter = centerPos - targetPos;

        // Handle edge case: target at center
        if (toCenter.LengthSquared() < 0.0001f)
        {
            return Vector3.Zero;
        }

        var dirToCenter = toCenter.Normalized();
        return dirToCenter * _pullStrength;
    }
}
