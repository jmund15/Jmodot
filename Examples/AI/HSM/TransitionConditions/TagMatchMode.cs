namespace Jmodot.Examples.AI.HSM.TransitionConditions;

/// <summary>
/// Defines how multiple required tags should be matched against result tags.
/// </summary>
public enum TagMatchMode
{
    /// <summary>
    /// OR logic - matches if ANY of the required tags is present in the result.
    /// </summary>
    Any,

    /// <summary>
    /// AND logic - matches only if ALL required tags are present in the result.
    /// </summary>
    All
}
