namespace Jmodot.Implementation.Physics.Collision;

using Godot;

/// <summary>
/// Slide collision response — persists along the surface (cancels gravity, dampens vertical velocity).
/// All durability fields (MaxCount, SelfDamage, VelocityRetention, FallbackResponse) are inherited.
/// </summary>
[Tool]
[GlobalClass]
public partial class SlideCollisionResponse : DurableCollisionResponse { }
