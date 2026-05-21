namespace Jmodot.Core.Interaction;

/// <summary>
/// Interface for world objects that respond to an in-place interaction (the configured
/// interact action) without being picked up or moved. Detected and dispatched by
/// <c>InteractorComponent3D</c>, which filters candidates via <see cref="CanInteract"/>
/// and invokes <see cref="Interact"/> on the nearest eligible target.
///
/// <para>Implementers MUST be <see cref="Node3D"/> — the dispatcher detects them via Area
/// signals and reads their <c>GlobalPosition</c> for nearest-target selection.</para>
/// </summary>
public interface IInteractable3D
{
    /// <summary>
    /// Whether this object can currently be interacted with by the given interactor.
    /// Used by the dispatcher to filter candidates before selection.
    /// </summary>
    bool CanInteract(Node3D interactor);

    /// <summary>
    /// Execute the interaction. Called when the interact action is pressed while this
    /// interactable is the nearest in-range target that passes <see cref="CanInteract"/>.
    /// The interactor is the entity node (e.g., the player), resolved from the Blackboard.
    /// </summary>
    void Interact(Node3D interactor);
}
