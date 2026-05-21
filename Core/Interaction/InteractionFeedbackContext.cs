namespace Jmodot.Core.Interaction;

using Jmodot.Core.Input;

/// <summary>
/// Immutable context handed to an <see cref="InteractionFeedbackStrategy"/> when its owning
/// interactable becomes the active interaction target. Carries only framework/Godot types —
/// no consumer coupling, so the abstraction stays Jmodot-pure.
/// </summary>
public readonly struct InteractionFeedbackContext
{
    /// <summary>The entity performing the interaction (e.g. the player).</summary>
    public Node3D Interactor { get; }

    /// <summary>The interactable's own node, used for world-space anchoring.</summary>
    public Node3D TargetNode { get; }

    /// <summary>The input action that triggers the interaction, for glyph resolution.</summary>
    public InputAction InteractAction { get; }

    public InteractionFeedbackContext(Node3D interactor, Node3D targetNode, InputAction interactAction)
    {
        this.Interactor = interactor;
        this.TargetNode = targetNode;
        this.InteractAction = interactAction;
    }
}
