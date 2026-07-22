namespace Jmodot.Implementation.Physics.Collision;

using Godot;
using Jmodot.Core.Physics;
using Jmodot.Core.Stats;
using Jmodot.Implementation.Combat;

/// <summary>
/// Pierce physics strategy — adds collision exception so the entity passes through the collider.
/// </summary>
[GlobalClass, Tool]
public partial class PiercePhysicsStrategy : CollisionPhysicsStrategy
{
    [ExportGroup("Collision Layers")]
    [Export(PropertyHint.Layers3DPhysics)]
    public uint PassThroughLayers { get; set; } = 0;

    [Export(PropertyHint.Layers3DPhysics)]
    public uint IgnoredLayers { get; set; } = 0;

    [ExportGroup("Speed")]
    [Export] public Attribute? SpeedAttribute { get; set; }

    public override PhysicsApplyResult Apply(ICollisionHost host, CollisionContact contact, float velocityRetention)
    {
        // Add collision exception so the entity doesn't physically collide with target again.
        // Routed through the managed registry so the destroy-time read sites never enumerate the
        // engine list (which spams under Jolt when a paired RID has been freed).
        if (host.GetUnderlyingNode() is PhysicsBody3D body &&
            contact.Collider is PhysicsBody3D target)
        {
            PhysicsCollisionExceptionRegistry.Add(body, target);
        }

        // Apply velocity retention (speed loss per pierce)
        var controller = host.Controller;
        controller.SetVelocity(controller.PreMoveVelocity * velocityRetention);

        return PhysicsApplyResult.Applied;
    }

    public override void ConfigureBody(ICollisionHost host, HitboxComponent3D? hitbox)
    {
        var body = host.GetUnderlyingNode() as CollisionObject3D;

        // PassThrough: Remove from physics body only (Hitbox still detects)
        if (body != null && PassThroughLayers != 0)
        {
            ApplyLayerMask(body, PassThroughLayers, false);
        }

        // Ignored: Remove from BOTH physics body AND Hitbox
        if (IgnoredLayers != 0)
        {
            if (body != null) { ApplyLayerMask(body, IgnoredLayers, false); }
            if (hitbox != null) { ApplyLayerMask(hitbox, IgnoredLayers, false); }
        }
    }

    private static void ApplyLayerMask(CollisionObject3D obj, uint mask, bool value)
    {
        for (int bit = 0; bit < 32 && mask != 0; bit++)
        {
            if ((mask & (1u << bit)) != 0)
            {
                obj.SetCollisionMaskValue(bit + 1, value);
                mask &= ~(1u << bit);
            }
        }
    }
}
