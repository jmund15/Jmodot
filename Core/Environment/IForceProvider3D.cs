namespace Jmodot.Core.Environment;

/// <summary>
///     A universal interface for any environmental object or area that can exert a
///     physical force on an actor. This allows for the creation of wind zones,
///     conveyor belts, rivers, or tractor beams in a modular and standardized way.
/// </summary>
public interface IForceProvider3D
{
    /// <summary>
    ///     Calculates the force vector this provider should apply to a specific target.
    /// </summary>
    /// <param name="target">The actor (e.g., CharacterBody3D) being affected.</param>
    /// <returns>A Vector3 representing the velocity to be added for this frame.</returns>
    Vector3 GetForceFor(Node3D target);

    /// <summary>
    ///     Whether this provider's force should count toward control-loss detection on
    ///     entities with a <c>ForceControlLossDetector</c>. Default false (ambient/movement-only,
    ///     e.g. gravity, conveyor belts). Capture sources (waves, tractor beams, gravity wells)
    ///     override to true. Source-side discrimination expressed as data on the provider's own
    ///     declaration site, no marker interfaces required.
    /// </summary>
    bool IsCaptureForce => false;
}
