namespace Jmodot.Implementation.Shared;

using Godot;

/// <summary>
/// Named physics-process priorities. Godot runs a higher <c>ProcessPhysicsPriority</c> later within a
/// physics frame; movement pumps sit at the default 0 and <see cref="GameClock"/> at
/// <see cref="int.MinValue"/>, so every value here runs after movement and after the clock steps.
/// </summary>
public static class PhysicsOrder
{
    /// <summary>Runs after movement pumps at the default 0, so it reads this tick's moved poses and slide collisions.</summary>
    public const int AfterMovement = 10;

    /// <summary>
    /// Runs after nodes at <see cref="AfterMovement"/>, so it reads the poses they wrote this tick.
    /// </summary>
    /// <remarks>
    /// Known gap: a segment chain whose head is itself a rider is not covered. The chain samples at
    /// <see cref="AfterMovement"/>, before the rider's follow at this priority.
    /// </remarks>
    public const int AfterFollowers = 20;

    /// <summary>Sets <paramref name="node"/>'s <c>ProcessPhysicsPriority</c> to <paramref name="priority"/> only when it is still 0, so a scene-authored order wins.</summary>
    public static void ApplyIfDefault(Node node, int priority)
    {
        if (node.ProcessPhysicsPriority == 0)
        {
            node.ProcessPhysicsPriority = priority;
        }
    }
}
