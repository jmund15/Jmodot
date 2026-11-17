namespace Jmodot.Core.AI.Perception;

using System.Diagnostics.CodeAnalysis;
using Godot;
using Jmodot.Core.AI.Perception;

/// <summary>
/// Use-time staleness guard for perceived target memories. A target can die, despawn or
/// decay between publication and consumption, so readers must recheck at use time.
/// </summary>
public static class PerceptionMemoryExts
{
    /// <summary>
    /// True when the memory is still confident (<see cref="Perception3DInfo.IsActive"/>) AND its
    /// target node is still alive — the precondition for aiming, firing, or resolving dispositions
    /// against <see cref="Perception3DInfo.Target"/>.
    /// </summary>
    public static bool IsLiveTarget([NotNullWhen(true)] this Perception3DInfo? memory)
        => memory is { IsActive: true } && GodotObject.IsInstanceValid(memory.Target);
}
