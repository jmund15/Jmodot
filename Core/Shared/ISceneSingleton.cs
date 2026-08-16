namespace Jmodot.Core.Shared;

using Godot;

/// <summary>
///     A Node that publishes itself as the one instance of its kind for the currently loaded scene, so consumers
///     that cannot hold a reference to it (Resources, spell effects, pooled components) can still reach it.
///     Dimension-agnostic: the constraint is <see cref="Node" />, so 2D and 3D systems implement the same contract.
/// </summary>
/// <remarks>
///     Contract an implementor MUST honour, none of which the signature can carry:
///     <list type="bullet">
///         <item>Claim <see cref="Instance" /> during its own initialization, and again in <c>_EnterTree</c> if it
///         is already initialized — a node re-entering the tree after a release would otherwise leave the slot empty.</item>
///         <item>Release the slot in <c>_ExitTree</c>, guarded by <c>Instance == this</c>, so an outgoing scene
///         cannot clear a claim the incoming scene already made.</item>
///         <item>Tolerate one frame of overlap: a scene swap adds the incoming instance before the outgoing one is
///         freed, so a replacement whose predecessor is queued for deletion is routine, not an anomaly.</item>
///     </list>
/// </remarks>
/// <typeparam name="TSelf">The implementing type itself.</typeparam>
public interface ISceneSingleton<TSelf>
    where TSelf : Node, ISceneSingleton<TSelf>
{
    /// <summary>The live instance for the current scene, or null when the scene hosts none.</summary>
    static abstract TSelf? Instance { get; }

    /// <summary>
    ///     Author-facing sentence appended to the absence warning, telling whoever reads the log how to make the
    ///     system exist. Lives on the implementor so the remedy has exactly one home instead of being re-authored
    ///     at every consumer.
    /// </summary>
    static abstract string AbsenceRemedy { get; }
}
