// --- IUtilityTask.cs ---
namespace Jmodot.Implementation.AI.UtilityAI;

/// <summary>
/// Contract for any BT action that can be used by the UtilitySelector.
/// </summary>
public interface IUtilityTask
{
    /// <summary>
    /// The consideration resource that calculates this action's score.
    /// </summary>
    public UtilityConsideration? Consideration { get; }

    /// <summary>
    /// Can this action be interrupted by a higher-scoring action?
    /// </summary>
    public bool Interruptible { get; }

    /// <summary>
    /// Used by the tie-breaker. Higher values win in case of a score tie.
    /// </summary>
    public int Priority { get; }
}
