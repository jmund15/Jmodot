namespace Jmodot.Implementation.Physics.Collision;

using Godot;
using Jmodot.Core.Physics;
using Jmodot.Implementation.Combat;

/// <summary>
/// Abstract exportable base for collision physics strategies. Exists so authored configs
/// can <c>[Export]</c> a strategy field — Godot cannot export the bare
/// <see cref="ICollisionPhysicsStrategy"/> interface, only a <see cref="Resource"/>-derived type.
/// Concrete strategies (Impact, Slide, Pierce) derive from this.
/// </summary>
[GlobalClass]
public abstract partial class CollisionPhysicsStrategy : Resource, ICollisionPhysicsStrategy
{
    /// <inheritdoc cref="ICollisionPhysicsStrategy.Apply"/>
    public abstract PhysicsApplyResult Apply(ICollisionHost host, CollisionContact contact, float velocityRetention);

    /// <inheritdoc cref="ICollisionPhysicsStrategy.ConfigureBody"/>
    public abstract void ConfigureBody(ICollisionHost host, HitboxComponent3D? hitbox);
}
