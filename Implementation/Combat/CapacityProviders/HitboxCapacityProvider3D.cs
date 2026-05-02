namespace Jmodot.Implementation.Combat.CapacityProviders;

using Godot;
using Jmodot.Core.Stats;

/// <summary>
/// Abstract base Resource for capacity-cap rules on <see cref="HitboxComponent3D"/>.
/// A capacity provider answers "can the hitbox accept one more hit right now?"
/// based on the hits accepted so far in the current attack session.
///
/// <para>
/// <b>All-must-agree contract:</b> a hitbox holds an
/// <c>[Export] Array&lt;HitboxCapacityProvider3D&gt;? CapacityProviders</c>; every entry
/// in the array must return <c>true</c> for a hit to be accepted. An empty/null array
/// means unlimited (no cap), preserving legacy behavior.
/// </para>
///
/// <para>
/// <b>Compose-not-override:</b> providers state independent rules and stack via logical
/// AND. They are not stages of a pipeline; order is irrelevant. Authors mix and match
/// concretes (e.g. <c>StatCapacityProvider3D + HealthCapacityProvider3D</c>) for cap
/// rules that compose cleanly.
/// </para>
///
/// <para>
/// <b>Dynamic re-evaluation:</b> the hitbox calls
/// <see cref="CanAcceptMoreHits"/> immediately before each accept AND immediately after
/// each accept (to deactivate synchronously when cap is exhausted). Concretes that read
/// runtime stats (e.g., a decrementing <c>pierce_count</c>) re-evaluate against current
/// state every call — there is no snapshot at attack start.
/// </para>
///
/// <para>
/// <b>Stateless:</b> like <see cref="Following.FollowModifier3D"/>, providers are
/// stateless Resources. The "hits accepted so far" state lives on the hitbox.
/// </para>
///
/// <para>
/// <b>Subclass rules:</b> concrete subclasses MUST be marked
/// <c>[GlobalClass, Tool]</c> — otherwise <c>.tres</c> files deserialize as bare
/// <see cref="Resource"/> and throw <see cref="System.InvalidCastException"/> on
/// type-checked access (per <c>Tool_Attribute_Cascade_Rules</c>).
/// </para>
/// </summary>
[GlobalClass, Tool]
public abstract partial class HitboxCapacityProvider3D : Resource
{
    /// <summary>
    /// Decide whether the hitbox can accept one more hit, given the hits already
    /// accepted in the current attack session.
    /// </summary>
    /// <param name="hitsAcceptedSoFar">Count of hits the hitbox has accepted since
    /// <c>StartAttack</c> (or since the last per-tick reset in continuous mode).</param>
    /// <param name="stats">The attacking spell's stat provider, if available. Concretes
    /// that read stats decide whether a missing <paramref name="stats"/> means fail-closed
    /// (no hits) or fail-open (unlimited) per their own contract.</param>
    /// <param name="owner">The hitbox owner Node, exposed for concretes that need to
    /// query owner-side state (e.g., a health-driven cap reading the spell's HP).</param>
    /// <returns><c>true</c> if this provider permits another hit; <c>false</c> to block.</returns>
    public abstract bool CanAcceptMoreHits(int hitsAcceptedSoFar, IStatProvider? stats, Node? owner);
}
