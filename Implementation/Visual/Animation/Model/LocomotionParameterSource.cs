namespace Jmodot.Implementation.Visual.Animation.Model;

using Godot;
using Jmodot.Core.Visual.Animation.Model;

/// <summary>
/// A resource that translates world-space velocity and model orientation into
/// parameters suitable for a 2D blend space in an AnimationTree, controlling locomotion.
/// </summary>
[GlobalClass]
public partial class LocomotionParameterSource : ModelAnimParameterSource
{
    [Export] public StringName BlendPositionParam { get; set; } = "locomotion/blend_position";
    [Export] public StringName SpeedParam { get; set; } = "locomotion/speed_scale";

    // Internal state is updated by the orchestrator, then read during UpdateParameters.
    private Vector2 _blendPosition = Vector2.Zero;
    private float _speed = 0f;

    /// <summary>
    /// Caches the latest input from the character controller.
    /// </summary>
    /// <param name="worldVelocity">The character's current velocity vector in world space.</param>
    /// <param name="modelForward">The character model's forward direction in world space.</param>
    public void SetInput(Vector3 worldVelocity, Vector3 modelForward)
    {
        _speed = worldVelocity.Length();
        if (_speed < 0.1f)
        {
            _blendPosition = Vector2.Zero;
            return;
        }

        // Calculate the velocity relative to the model's forward direction.
        Vector3 localVel = worldVelocity.Normalized().Rotated(Vector3.Up, -modelForward.SignedAngleTo(Vector3.Forward, Vector3.Up));

        // Map the 3D local velocity (X,Z) to a 2D blend space position (X,Y).
        // The Z component is negated because in Godot's 3D space, -Z is "forward".
        _blendPosition = new Vector2(localVel.X, -localVel.Z);
    }

    /// <summary>
    /// Applies the cached locomotion state to the AnimationTree controller.
    /// </summary>
    public override void UpdateParameters(IAnim3DController controller)
    {
        controller.SetParameter(BlendPositionParam, _blendPosition);
        controller.SetParameter(SpeedParam, _speed);
    }
}
