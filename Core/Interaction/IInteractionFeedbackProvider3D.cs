namespace Jmodot.Core.Interaction;

/// <summary>
/// Opt-in capability for an <see cref="IInteractable3D"/> that also exposes feedback
/// (button prompt, outline, glow, etc.). Kept separate from <see cref="IInteractable3D"/>
/// so the core interaction contract stays minimal — interactables needing no feedback
/// implement only <see cref="IInteractable3D"/>. The dispatcher queries for this interface
/// to decide whether to drive the feedback lifecycle.
/// </summary>
public interface IInteractionFeedbackProvider3D
{
    /// <summary>The feedback strategy this interactable owns, or null for no feedback.</summary>
    InteractionFeedbackStrategy? FeedbackStrategy { get; }
}
