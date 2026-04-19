namespace Jmodot.Core.Actors;

/// <summary>
/// 2D twin of <see cref="ForceContext"/>. Written to the blackboard alongside the
/// ControlLost flag to describe the dominant force that caused control loss.
/// </summary>
public class ForceContext2D
{
    /// <summary>Direction of the strongest force affecting the entity.</summary>
    public Godot.Vector2 DominantForceDirection;

    /// <summary>The already-scaled force magnitude the entity actually experiences.</summary>
    public float ForceMagnitude;

    /// <summary>The Area2D providing the strongest force (for damage attribution).</summary>
    public Godot.Node2D? DominantSource;
}
