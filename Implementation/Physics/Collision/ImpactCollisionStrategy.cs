namespace Jmodot.Implementation.Physics.Collision;

using Godot;
using Jmodot.Core.AI.BB;
using Jmodot.Core.Physics;
using Jmodot.Implementation.AI.BB;
using Jmodot.Implementation.Combat;
using Jmodot.Implementation.Physics;
using Jmodot.Implementation.Shared;

/// <summary>
/// Unified collision physics strategy using elastic collision resolution.
/// Physics-correct bounce and entity-entity elastic collision resolution.
///
/// Two paths through one formula (ImpactPhysics.ResolveElasticCollision):
/// 1. Entity-entity: Both entities have IImpactable → combined COR, real mass/velocity,
///    deduplication via ImpactFrameTracker, both velocities updated.
/// 2. Surface (fallback): Collider has no IImpactable → stabilityB=MAX, velocityB=Zero,
///    velocityRetention used as COR → formula degenerates to simple reflection * COR.
///
/// BounceRandomness stays on this strategy (collision-level config, not entity property).
/// </summary>
[GlobalClass, Tool]
public partial class ImpactCollisionStrategy : CollisionPhysicsStrategy
{
    [Export(PropertyHint.Range, "0,1")] public float BounceRandomness { get; set; } = 0f;

    public override PhysicsApplyResult Apply(ICollisionHost host, CollisionContact contact, float velocityRetention)
    {
        var controller = host.Controller;

        var normal = contact.Normal;
        var incomingVelocity = controller.PreMoveVelocity;

        // Discover self's IImpactable from host's blackboard
        TryGetImpactable(host.GetUnderlyingNode(), out var selfImpactable);

        // Discover target's IImpactable from collider's blackboard
        bool targetIsElastic = TryGetImpactable(contact.Collider, out var targetImpactable)
            && targetImpactable!.ParticipatesInElasticCollisions
            && selfImpactable != null
            && selfImpactable.ParticipatesInElasticCollisions;

        ImpactResult result;

        if (targetIsElastic)
        {
            // Entity-entity elastic collision — deduplicate via frame tracker
            ulong selfId = host.GetUnderlyingNode().GetInstanceId();
            ulong targetId = contact.Collider.GetInstanceId();

            if (!ImpactFrameTracker.TryClaimPair(selfId, targetId))
            {
                return PhysicsApplyResult.Skipped; // Pair already resolved by the other entity
            }

            float combinedCor = ImpactPhysics.CombineRestitution(
                selfImpactable!.BounceRestitution, targetImpactable!.BounceRestitution);

            result = ImpactPhysics.ResolveElasticCollision(
                incomingVelocity, targetImpactable.Velocity,
                selfImpactable.Stability, targetImpactable.Stability,
                normal, combinedCor);

            if (!result.IsValid)
            {
                return PhysicsApplyResult.Skipped; // Separating — persist without physics
            }

            // Apply to target via IImpactable (additive delta preserves other forces)
            targetImpactable.ApplyImpactVelocity(result.NewVelocityB);
        }
        else
        {
            // Surface bounce — velocityRetention used as COR, wall has infinite mass
            float selfStability = selfImpactable?.Stability ?? 0f;

            result = ImpactPhysics.ResolveElasticCollision(
                incomingVelocity, Vector3.Zero,
                selfStability, float.MaxValue,
                normal, velocityRetention);

            if (!result.IsValid)
            {
                return PhysicsApplyResult.Skipped; // Separating — persist without physics
            }
        }

        // Apply reflected velocity to self
        var finalVelocity = result.NewVelocityA;

        if (BounceRandomness > 0)
        {
            finalVelocity = ApplyRandomnessCone(finalVelocity, normal);
        }

        controller.SetVelocity(finalVelocity);
        return PhysicsApplyResult.Applied;
    }

    private Vector3 ApplyRandomnessCone(Vector3 reflectedVelocity, Vector3 normal)
    {
        const float MaxBounceDeviationDeg = 45f;
        float maxDeviationDeg = MaxBounceDeviationDeg * BounceRandomness;
        float randomAngle = JmoRng.NonDeterministic().GetRndInRange(-maxDeviationDeg, maxDeviationDeg);
        float randomAngleRad = Mathf.DegToRad(randomAngle);

        Vector3 rotationAxis = normal.Cross(reflectedVelocity).Normalized();
        if (rotationAxis.IsZeroApprox())
        {
            rotationAxis = Vector3.Up.Cross(reflectedVelocity).Normalized();
            if (rotationAxis.IsZeroApprox())
            {
                rotationAxis = Vector3.Right.Cross(reflectedVelocity).Normalized();
            }
        }

        if (!rotationAxis.IsZeroApprox())
        {
            reflectedVelocity = reflectedVelocity.Rotated(rotationAxis, randomAngleRad);
        }

        return reflectedVelocity;
    }

    private static bool TryGetImpactable(Node node, out IImpactable? target)
    {
        target = null;
        if (!node.TryGetFirstChildOfInterface<IBlackboard>(out var bb) || bb == null)
        {
            return false;
        }
        return bb.TryGet(BBDataSig.PhysicsInteraction, out target) && target != null;
    }

    public override void ConfigureBody(ICollisionHost host, HitboxComponent3D? hitbox)
    {
        // Impact strategy doesn't need body configuration
    }
}
