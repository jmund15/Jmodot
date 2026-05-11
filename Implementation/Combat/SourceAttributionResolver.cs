namespace Jmodot.Implementation.Combat;

using Core.Combat;
using Core.Combat.Reactions;
using Godot;
using Implementation.Actors;

/// <summary>
/// Pure-function attribution resolver: maps an <see cref="ImpactInfo"/> to the most
/// plausible damage-source <see cref="Node"/>. Used by <c>ForceImpactDamageApplier</c>
/// to credit wall-slam damage to the launching spell/actor instead of the wall itself.
/// </summary>
/// <remarks>
/// <para>
/// Three-step chain (in order of preference):
/// </para>
/// <list type="number">
///   <item>Most recent <see cref="KnockbackResult"/> in <paramref name="combatLog"/>
///         within <paramref name="windowSeconds"/> — typically the spell or actor that
///         just knocked the target into the wall.</item>
///   <item>Dominant sustained force from <paramref name="forceReceiver"/> — wave drag,
///         conveyor, magnet, future fluid currents.</item>
///   <item><c>info.Collider</c> — last resort, the wall itself.</item>
/// </list>
/// <para>
/// Extracted as a static helper so the chain ordering, window expiry, and
/// null-degradation paths are unit-testable independently of the Node lifecycle of
/// <c>ForceImpactDamageApplier</c>. Logic Domain — strict TDD applies.
/// </para>
/// </remarks>
public static class SourceAttributionResolver
{
    public static Node? Resolve(
        ImpactInfo info,
        CombatLog? combatLog,
        ExternalForceReceiver3D? forceReceiver,
        Node3D? self,
        float windowSeconds)
    {
        if (combatLog != null)
        {
            var latest = combatLog.GetMostRecent<KnockbackResult>(windowSeconds);
            if (latest?.Source != null)
            {
                return latest.Source;
            }
        }

        if (forceReceiver != null && self != null
            && GodotObject.IsInstanceValid(forceReceiver))
        {
            var (dominant, _) = forceReceiver.GetDominantForceSource(self);
            if (dominant != null)
            {
                return dominant;
            }
        }

        return info.Collider;
    }
}
