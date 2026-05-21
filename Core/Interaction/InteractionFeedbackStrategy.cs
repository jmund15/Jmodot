namespace Jmodot.Core.Interaction;

/// <summary>
/// Abstract Resource strategy that renders interaction feedback for a targeted interactable
/// (button prompt now; outline/glow/composition later). The interactable owns its strategy
/// instance via <see cref="IInteractionFeedbackProvider3D.FeedbackStrategy"/>; the dispatcher
/// drives the lifecycle: <see cref="OnTargeted"/> when it becomes the active target,
/// <see cref="OnProcess"/> each frame while targeted, <see cref="OnUntargeted"/> when it loses
/// target. Follows the Resource-Strategy precedent so designers swap strategies in the Inspector
/// without code changes.
/// </summary>
[GlobalClass]
public abstract partial class InteractionFeedbackStrategy : Resource
{
    /// <summary>Called once when the owning interactable becomes the active interaction target.</summary>
    public abstract void OnTargeted(in InteractionFeedbackContext ctx);

    /// <summary>Called once when the owning interactable stops being the active target.</summary>
    public abstract void OnUntargeted();

    /// <summary>Per-frame hook while targeted (e.g. world→screen anchoring). No-op by default.</summary>
    public virtual void OnProcess(double delta) { }
}
