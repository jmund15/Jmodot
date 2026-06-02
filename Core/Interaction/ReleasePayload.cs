namespace Jmodot.Core.Interaction;

using Godot;
using Jmodot.Core.Stats;

/// <summary>
/// Immutable description of a single release of a held object, carried by the typed
/// <see cref="IThrowable3D.OnThrownWithPayload"/> event. A runtime value (plain record, not a
/// <c>[GlobalClass]</c> resource — mirrors <c>CombatPayload</c>): the release verb constructs it,
/// the grabbable applies its <see cref="LaunchVelocity"/>, and downstream listeners read the
/// classification and originating stats without coupling to a holder or HSM state.
/// </summary>
/// <param name="Kind">How the object left control (throw / drop / stash).</param>
/// <param name="LaunchVelocity">World-space velocity to apply to the body on release; <see cref="Vector3.Zero"/> for a passive drop.</param>
/// <param name="Thrower">Stats of the entity that initiated the release, for downstream scaling (damage, etc.). Null when the releaser has no stats — a valid state, not an error.</param>
public record ReleasePayload(ReleaseKind Kind, Vector3 LaunchVelocity, IStatProvider? Thrower);
