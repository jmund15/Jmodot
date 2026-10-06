namespace Jmodot.Core.Physics;

/// <summary>
/// A body that movers push by walking into it. Pushing is opt-in on the pushed side: a mover pushes only a
/// collider that implements this, so bodies that do not implement it are never moved by contact.
/// </summary>
/// <remarks>
/// <see cref="Jmodot.Implementation.Actors.MovementProcessor2D"/> calls <see cref="ReceivePush"/> during the
/// MOVER's physics step, right after the mover's own move, once for every slide collision the mover reported
/// against this body that step. A body can therefore receive several pushes in one frame and must compose them
/// itself (<see cref="PushContact2D.ResolveVelocityChange"/> resolves against the pushed body's current velocity,
/// so repeated contacts converge rather than stack). The implementer must not free the mover or itself
/// synchronously: the mover is still iterating its slide collisions.
/// Dimension-parallel sibling: <see cref="IPushable3D"/>.
/// </remarks>
public interface IPushable2D
{
    void ReceivePush(in PushContact2D push);
}
