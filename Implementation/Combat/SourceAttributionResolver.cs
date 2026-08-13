namespace Jmodot.Implementation.Combat;

using Core.Actors;
using Core.Combat;
using Core.Combat.Reactions;
using Godot;
using Implementation.Actors;

/// <summary>
/// Pure-function attribution resolver: maps an <see cref="ImpactInfo"/> to the most
/// plausible damage-source <see cref="Node"/>.
/// </summary>
/// <remarks>
/// <para>
/// Three-step chain (in order of preference):
/// </para>
/// <list type="number">
///   <item>Most recent <see cref="KnockbackResult"/> in the supplied <c>combatLog</c>
///         within <c>windowSeconds</c> — typically the spell or actor that
///         just knocked the target into the wall.</item>
///   <item>Dominant sustained force from the supplied <c>forceReceiver</c> — wave drag,
///         conveyor, magnet, fluid currents.</item>
///   <item><c>info.Collider</c> — last resort, the wall itself.</item>
/// </list>
/// <para>
/// Extracted as a static helper so the chain ordering, window expiry, and
/// null-degradation paths are unit-testable independently of the Node lifecycle of
/// <see cref="ForceImpactDamageApplier"/>.
/// </para>
/// </remarks>
public static class SourceAttributionResolver
{
    public static Node? Resolve(
        ImpactInfo info,
        CombatLog? combatLog,
        IExternalForceReceiver? forceReceiver,
        Node3D? self,
        float windowSeconds)
        => ResolveWithCause(info, combatLog, forceReceiver, self, windowSeconds).Source;

    /// <summary>
    /// Same chain as <see cref="Resolve"/>, additionally classifying WHICH step attributed
    /// the impact. <see cref="ImpactCause.ColliderFallback"/> means no external evidence was
    /// found — the impact was caused by the actor's own movement (attack lunge, leap landing,
    /// voluntary fall), which consumers like <see cref="ForceImpactDamageApplier"/> use to gate
    /// self-damage out of force-driven damage application.
    /// </summary>
    public static (Node? Source, ImpactCause Cause) ResolveWithCause(
        ImpactInfo info,
        CombatLog? combatLog,
        IExternalForceReceiver? forceReceiver,
        Node3D? self,
        float windowSeconds)
    {
        if (combatLog != null)
        {
            var latest = combatLog.GetMostRecent<KnockbackResult>(windowSeconds);
            if (latest?.Source != null)
            {
                return (latest.Source, ImpactCause.Knockback);
            }
        }

        if (forceReceiver != null && self != null
            && (forceReceiver is not GodotObject receiverObj || GodotObject.IsInstanceValid(receiverObj)))
        {
            var dominant = forceReceiver.GetDominantForceSourceNode(self);
            if (dominant != null)
            {
                return (dominant, ImpactCause.SustainedForce);
            }
        }

        return (info.Collider, ImpactCause.ColliderFallback);
    }
}

/// <summary>
/// Which attribution step credited an impact. <see cref="ColliderFallback"/> is the
/// no-external-evidence case: the actor's own movement produced the collision.
/// </summary>
public enum ImpactCause
{
    Knockback,
    SustainedForce,
    ColliderFallback,
}
