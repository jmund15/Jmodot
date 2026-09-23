namespace Jmodot.Core.Interaction;

using Jmodot.Core.Shared;

/// <summary>
/// Abstract Resource strategy that renders interaction feedback for a targeted interactable
/// (button prompt now; outline/glow/composition later). The interactable owns its strategy
/// instance via <see cref="IInteractionFeedbackProvider3D.FeedbackStrategy"/>; the dispatcher
/// drives the lifecycle: <see cref="OnTargeted"/> when it becomes the active target,
/// <see cref="OnProcess"/> each frame while targeted, <see cref="OnUntargeted"/> when it loses
/// target. Follows the Resource-Strategy precedent so designers swap strategies in the Inspector
/// without code changes.
/// </summary>
[GlobalClass, Tool]
public abstract partial class InteractionFeedbackStrategy : Resource, IResourceConfigurationWarnings
{
    /// <summary>Called once when the owning interactable becomes the active interaction target.</summary>
    public abstract void OnTargeted(in InteractionFeedbackContext ctx);

    /// <summary>Called once when the owning interactable stops being the active target.</summary>
    public abstract void OnUntargeted();

    /// <summary>Per-frame hook while targeted (e.g. world→screen anchoring). No-op by default.</summary>
    public virtual void OnProcess(double delta) { }

    /// <summary>
    /// Authoring faults in this strategy, empty by default. Every interactable that exports a
    /// strategy forwards this from its own <c>_GetConfigurationWarnings</c>, so a subclass with
    /// validation overrides it and is surfaced by every host with no host-side change.
    /// </summary>
    public virtual string[] GetResourceConfigurationWarnings() => [];

    /// <summary>
    /// Authoring faults in how <paramref name="host"/> is configured for this strategy, empty by
    /// default. Every interactable that exports a strategy forwards this from its own
    /// <c>_GetConfigurationWarnings</c>, passing itself, so a strategy that needs something from its
    /// host reports it without a host-side type test. Runs in the editor on hosts that may be outside
    /// the tree and never initialized: read the host's authored state only, and never mutate it.
    /// </summary>
    public virtual string[] GetHostConfigurationWarnings(Node host) => [];
}
