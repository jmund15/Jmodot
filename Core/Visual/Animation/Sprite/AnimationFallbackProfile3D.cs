namespace Jmodot.Core.Visual.Animation.Sprite;

using System.Collections.Generic;
using Godot;
using Movement;

/// <summary>
/// The entity scope of the base-clip question: what a 3D body looks like on the frames no narrower
/// scope claims the animator. One rung per signal the body reads — ground speed today, a stance or
/// posture tomorrow — selected per entity as a single Inspector slot on the claim resolver.
/// </summary>
/// <remarks>
/// A profile is a shared, process-cached Resource: two entities assigned the same `.tres` hold the
/// SAME instance, so an implementation must carry no per-consumer mutable state. Everything a rung
/// needs to decide is passed in per call, and anything it must remember between frames has to be
/// recoverable from <c>previousResolvedClip</c>. A rung that integrates over time — a bob phase, a
/// dwell timer — cannot be expressed on this base; that is a stated limit of the one-slot design,
/// not an oversight.
/// </remarks>
[GlobalClass, Tool]
public abstract partial class AnimationFallbackProfile3D : Resource
{
    /// <summary>
    /// The clip this tier wants playing, or null to claim nothing and leave the animator alone.
    /// Called every physics frame — including frames a narrower scope wins — so the tier's own
    /// signal tracks real motion and a hand-back reads the body's current state.
    /// </summary>
    /// <param name="controller">The entity's body. Read-only here: a fallback tier observes, never drives.</param>
    /// <param name="delta">The physics frame delta, for rungs whose signal is rate-based.</param>
    /// <param name="previousResolvedClip">
    /// This tier's OWN previous return value, held by the resolver independently of arbitration.
    /// Null only on the first frame.
    /// </param>
    public abstract StringName? ResolveClip(ICharacterController3D controller, float delta, StringName? previousResolvedClip);

    /// <summary>
    /// Every clip this profile can return. The claim resolver checks each against the entity's own
    /// animator at editor time, so a clip the animator does not author is named before playtest.
    /// </summary>
    public abstract IEnumerable<StringName> AuthoredClips { get; }

    /// <summary>
    /// A human-readable description of what is misconfigured, or null when the profile is valid.
    /// Surfaced on BOTH channels by the resolver — scene-dock warning and a startup log line —
    /// because a `.tres`-only edit never opens the scene that would show the warning.
    /// </summary>
    public abstract string? ValidateConfiguration();
}
