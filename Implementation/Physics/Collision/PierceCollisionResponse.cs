namespace Jmodot.Implementation.Physics.Collision;

using Godot;

/// <summary>
/// Pierce collision response — passes through the collider (adds collision exception).
/// All durability fields (MaxCount, SelfDamage, VelocityRetention, FallbackResponse) are inherited.
/// </summary>
[Tool]
[GlobalClass]
public partial class PierceCollisionResponse : DurableCollisionResponse { }
