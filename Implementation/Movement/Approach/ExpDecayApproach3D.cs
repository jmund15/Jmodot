namespace Jmodot.Implementation.Movement.Approach;

using Godot;
using Jmodot.Implementation.Shared;

/// <summary>
/// Frame-rate-independent exponential approach: the mover covers a fixed fraction of the remaining gap
/// each second, so it decelerates as it closes. Trip time is emergent, not authored — use
/// <see cref="FixedDurationApproach3D"/> when arrival must land on a schedule.
/// </summary>
[GlobalClass, Tool]
public partial class ExpDecayApproach3D : ApproachProfile3D
{
    /// <summary>Decay rate in 1/s. Higher converges faster; 0 never moves.</summary>
    [Export] public float Speed { get; private set; } = 8f;

    /// <summary>Distance from the target at which the approach counts as arrived.</summary>
    [Export] public float ArrivalDistance { get; private set; } = 0.1f;

    public override Vector3 Step(Vector3 current, Vector3 target, float elapsed, float delta)
    {
        return JmoMath.ExpDecay(current, target, Speed, delta);
    }

    public override bool IsComplete(Vector3 current, Vector3 target, float elapsed)
    {
        return current.DistanceTo(target) <= ArrivalDistance;
    }

    #region Test Helpers
#if TOOLS
    internal void SetSpeedForTest(float speed) => Speed = speed;
    internal void SetArrivalDistanceForTest(float distance) => ArrivalDistance = distance;
#endif
    #endregion
}
