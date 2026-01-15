namespace Jmodot.Implementation.Environment;

using Godot;
using Core.Environment;

/// <summary>
/// A force provider that applies gravity to entities within the area.
/// Uses ProjectSettings for gravity direction and magnitude, with configurable scale.
/// Used for arena-wide gravity or localized gravity zones.
/// </summary>
[GlobalClass]
public partial class GravityForceArea : Area3D, IForceProvider3D
{
    /// <summary>
    /// Multiplier for the global gravity value.
    /// 1.0 = normal gravity, 0.5 = half gravity, 2.0 = double gravity, -1.0 = anti-gravity.
    /// </summary>
    [Export] public float GravityScale { get; set; } = 1.0f;

    /// <summary>
    /// Returns the gravity force to apply to the target.
    /// Force = ProjectSettings.Gravity * GravityScale
    /// </summary>
    public Vector3 GetForceFor(Node3D target)
    {
        if (target == null)
        {
            return Vector3.Zero;
        }

        if (GravityScale == 0.0f)
        {
            return Vector3.Zero;
        }

        // Fetch global gravity from ProjectSettings
        var gravityVec = ProjectSettings.GetSetting("physics/3d/default_gravity_vector").AsVector3();
        var gravityMag = ProjectSettings.GetSetting("physics/3d/default_gravity").AsSingle();

        return gravityVec * gravityMag * GravityScale;
    }
}
