namespace Jmodot.Implementation.Physics.Collision;

using Godot;

/// <summary>
/// Abstract base for all collision responses. Serves as the polymorphic type
/// for fields that accept either destroy or durable responses.
/// </summary>
[Tool]
[GlobalClass]
public abstract partial class BaseCollisionResponse : Resource { }
