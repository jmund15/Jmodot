namespace Jmodot.Implementation.Interaction;

using System;
using Godot;
using Jmodot.Core.Interaction;
using Jmodot.Core.Shared.Attributes;

/// <summary>
/// Generic press-interact primitive: an <see cref="Area3D"/> a designer drops on a scene fixture
/// (lever, altar, pressure plate). Discovered by <c>InteractorComponent3D</c>'s area-overlap detection
/// (it casts the overlapping Area3D directly to <see cref="IInteractable3D"/>), so the node MUST carry a
/// <c>CollisionShape3D</c> child to define its detection zone. On <see cref="Interact"/> it fires
/// <see cref="Fired"/> with its authored <see cref="InteractionId"/> and the interactor — carrying NO
/// knowledge of any consuming system (the framework boundary: a relay on the consumer side routes the
/// signal onward). Also an <see cref="IInteractionFeedbackProvider3D"/> so the interactor's targeting
/// feedback (prompt/highlight) works through the optional <see cref="FeedbackStrategy"/>.
/// </summary>
[GlobalClass]
public partial class SignallingInteractableComponent3D : Area3D, IInteractable3D, IInteractionFeedbackProvider3D
{
    /// <summary>Identifier fired on interaction. Consumers match against this by value.</summary>
    [Export, RequiredExport] public StringName InteractionId { get; private set; } = null!;

    /// <summary>When false, <see cref="CanInteract"/> returns false so the interactor skips this target.</summary>
    [Export] public bool Enabled { get; set; } = true;

    /// <summary>Optional targeting feedback (prompt/highlight) driven by InteractorComponent3D's OnTargeted/OnUntargeted.</summary>
    [Export] public InteractionFeedbackStrategy? FeedbackStrategy { get; private set; }

    public event Action<StringName, Node3D> Fired = delegate { };

    public override void _Ready() => this.ValidateRequiredExports();

    public bool CanInteract(Node3D interactor) => Enabled;

    public void Interact(Node3D interactor) => Fired.Invoke(InteractionId, interactor);

    #region Test Helpers
#if TOOLS
    internal void SetInteractionId(StringName value) => InteractionId = value;
    internal void SetEnabled(bool value) => Enabled = value;
#endif
    #endregion
}
