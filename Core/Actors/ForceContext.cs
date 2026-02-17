namespace Jmodot.Core.Actors;

/// <summary>
/// Context data written to the blackboard alongside the ControlLost flag.
/// Describes the dominant force that caused control loss.
/// </summary>
public struct ForceContext
{
    /// <summary>Direction of the strongest force affecting the entity.</summary>
    public Godot.Vector3 DominantForceDirection;

    /// <summary>The already-scaled force magnitude the entity actually experiences.</summary>
    public float ForceMagnitude;

    /// <summary>The Area3D providing the strongest force (for damage attribution).</summary>
    public Godot.Node3D? DominantSource;
}
