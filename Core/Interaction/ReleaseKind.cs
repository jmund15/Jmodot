namespace Jmodot.Core.Interaction;

/// <summary>
/// Classifies how a held object leaves a holder's control, so a single typed release event can
/// distinguish a throw (launch impulse applied) from a passive drop or a stash into inventory.
/// </summary>
public enum ReleaseKind
{
    /// <summary>Released with a launch impulse along the holder's aim (see <see cref="Jmodot.Core.Movement.ILaunchable3D"/>).</summary>
    Throw,

    /// <summary>Released in place with no launch impulse; the body's own physics takes over.</summary>
    Drop,

    /// <summary>Removed from the world into a holder-owned store (inventory) rather than dropped.</summary>
    Stash,
}
