namespace Jmodot.Implementation.Physics.Collision;

using Godot;

/// <summary>
/// Bounce collision response — reflects velocity off the collision surface.
/// All durability fields (MaxCount, SelfDamage, VelocityRetention, FallbackResponse) are inherited.
/// </summary>
[Tool]
[GlobalClass]
public partial class BounceCollisionResponse : DurableCollisionResponse { }
