namespace Jmodot.Core.Visual.Animation.Sprite;

/// <summary>
/// How a composite slave slot degrades when its animator lacks the exact directional clip
/// the orchestrator requested. Both policies hide the slot's visuals (via
/// <c>StopAnim</c> → <c>AnimationVisibilityCoordinator.HideAllOnStop</c>) when nothing resolves.
/// </summary>
public enum SlotFallbackPolicy
{
    /// <summary>
    /// Degrade through the nearest available directional clip (by angular proximity to the
    /// current facing), then the undirected base clip. Lets a 4-directional art set serve an
    /// 8-direction request. Default.
    /// </summary>
    NearestDirectional = 0,

    /// <summary>
    /// Only play the exact directional clip or the undirected base clip — no nearest-directional
    /// degradation. Used when a partial/single-direction art set would look wrong reused across
    /// facings; the slot hides rather than showing a mismatched direction.
    /// </summary>
    HideSlot = 1,
}
