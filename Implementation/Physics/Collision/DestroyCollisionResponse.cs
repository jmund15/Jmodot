namespace Jmodot.Implementation.Physics.Collision;

using Godot;

/// <summary>
/// Lightweight destroy response — the type itself IS the behavior: "spell is destroyed."
/// No fields needed; any collision resolved to this type terminates the spell.
/// </summary>
[Tool]
[GlobalClass]
public partial class DestroyCollisionResponse : BaseCollisionResponse { }
