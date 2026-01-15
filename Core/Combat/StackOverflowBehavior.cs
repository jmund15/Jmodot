namespace Jmodot.Core.Combat;

/// <summary>
/// Defines behavior when MaxStacks is reached and a new effect tries to apply.
/// </summary>
public enum StackOverflowBehavior
{
    /// <summary>Reject the incoming effect entirely. It does not apply.</summary>
    Reject = 0,

    /// <summary>Refresh duration of the oldest stack, reject new instance.</summary>
    RefreshOldest = 1,

    /// <summary>Remove the oldest stack and add the new one.</summary>
    ReplaceOldest = 2
}
