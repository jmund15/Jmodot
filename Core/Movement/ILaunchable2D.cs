namespace Jmodot.Core.Movement;

using Godot;

/// <summary>
/// 2D parity mirror of <see cref="ILaunchable3D"/>: a 2D physics body that can receive a one-shot
/// launch impulse, pairing with <see cref="ICharacterController2D"/>. Interface-only for now (no
/// 2D interaction family consumes it yet — that arrives with the 2D hold/throw build-out); shipped
/// alongside the 3D capability to satisfy Jmodot's 2D/3D parity rule.
/// </summary>
public interface ILaunchable2D
{
    /// <summary>Apply <paramref name="velocity"/> to the body as its post-release motion (world space, units/sec).</summary>
    void Launch(Vector2 velocity);
}
