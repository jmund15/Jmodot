namespace Jmodot.Core.Physics;

using Godot;

/// <summary>
/// One mover-into-body contact, as the mover saw it: who pushed, how fast it was moving before the contact
/// blocked it, and the contact normal (pointing from the pushed body toward the mover, as Godot reports it).
/// Dimension-parallel sibling: <see cref="PushContact2D"/>.
/// </summary>
public readonly record struct PushContact3D(Node3D Pusher, Vector3 PusherVelocity, Vector3 Normal)
{
    /// <summary>
    /// The velocity change the pushed body takes: <paramref name="transferRatio"/> times the speed at which the
    /// pusher is closing on the body along the contact, directed away from the pusher. Zero when the two are
    /// separating or the ratio is not positive. With a ratio of 1 the body ends up matching the pusher's speed
    /// along the contact; repeated contacts in the same push therefore converge instead of accumulating.
    /// </summary>
    public Vector3 ResolveVelocityChange(Vector3 pushedVelocity, float transferRatio)
    {
        if (!(transferRatio > 0f)) { return Vector3.Zero; }

        var direction = -this.Normal;
        float closingSpeed = (this.PusherVelocity - pushedVelocity).Dot(direction);
        if (!(closingSpeed > 0f)) { return Vector3.Zero; }

        return direction * (closingSpeed * transferRatio);
    }
}
