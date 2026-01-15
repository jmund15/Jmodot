namespace Jmodot.Core.Combat;

/// <summary>
/// Result of stack policy evaluation. Immutable struct indicating what action to take.
/// </summary>
public readonly struct StackPolicyResult
{
    /// <summary>
    /// Whether the new effect instance should be added.
    /// </summary>
    public bool IsAccepted { get; }

    /// <summary>
    /// Whether the oldest existing instance should have its duration refreshed.
    /// </summary>
    public bool ShouldRefreshOldest { get; }

    /// <summary>
    /// Whether the oldest existing instance should be removed to make room.
    /// </summary>
    public bool ShouldReplaceOldest { get; }

    private StackPolicyResult(bool accepted, bool refresh, bool replace)
    {
        IsAccepted = accepted;
        ShouldRefreshOldest = refresh;
        ShouldReplaceOldest = replace;
    }

    /// <summary>Accept the new effect instance.</summary>
    public static StackPolicyResult Accept() => new(true, false, false);

    /// <summary>Reject the new effect instance entirely.</summary>
    public static StackPolicyResult Reject() => new(false, false, false);

    /// <summary>Refresh the oldest instance's duration, reject new instance.</summary>
    public static StackPolicyResult RefreshOldest() => new(false, true, false);

    /// <summary>Remove oldest instance, accept new instance.</summary>
    public static StackPolicyResult ReplaceOldest() => new(true, false, true);
}
