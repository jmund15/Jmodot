namespace Jmodot.Implementation.Physics.Collision;

using Godot;

/// <summary>
/// Ignore collision response — no physics interaction; lets other systems handle
/// (e.g., ReactionComponent for spell-vs-spell).
/// Inherits durability fields for count tracking, self-damage, and fallback chains.
/// VelocityRetention defaults to 1.0 (identity — unused but harmless).
/// </summary>
[Tool]
[GlobalClass]
public partial class IgnoreCollisionResponse : DurableCollisionResponse { }
