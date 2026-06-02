namespace Jmodot.Core.Movement;

using Godot;

/// <summary>
/// Body-movement capability for a 3D physics body that can receive a one-shot launch impulse,
/// queried on the body node itself (<c>PhysicalBody as ILaunchable3D</c>). Pairs with
/// <see cref="ICharacterController3D"/> as the launch counterpart to its per-frame velocity
/// driver: it lets the interaction layer apply a throw velocity directly to the body, keeping
/// throw HSM-agnostic (no state owns the launch). A body that does not implement this interface
/// is not launchable, so a throw release no-ops against it.
/// </summary>
public interface ILaunchable3D
{
    /// <summary>Apply <paramref name="velocity"/> to the body as its post-release motion (world space, units/sec).</summary>
    void Launch(Vector3 velocity);
}
