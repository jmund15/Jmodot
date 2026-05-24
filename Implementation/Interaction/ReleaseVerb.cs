namespace Jmodot.Implementation.Interaction;

using Godot;
using Jmodot.Core.Interaction;
using Jmodot.Core.Input;
using Jmodot.Core.Shared.Attributes;

/// <summary>
/// A modular held-item release affordance offered by an <see cref="IHolder3D"/>. Pairs a
/// trigger <see cref="InputAction"/> with a release behavior and a capability gate. The set of
/// verbs a holder exports is its release-affordance vocabulary (drop-only = a one-element list);
/// object capability (<see cref="IThrowable3D"/>/<see cref="IDroppable3D"/>) is the orthogonal axis,
/// gated by <see cref="CanRelease"/>.
///
/// <para>Keyed on <see cref="IGrabbable3D"/> rather than a raw <see cref="Node3D"/>: the throw/drop
/// capability interfaces live on the child capability component, not the reparented body root that
/// the holder physically holds. Concrete verbs release via the grabbable, then stop the hold on
/// <see cref="IGrabbable3D.PhysicalBody"/>.</para>
/// </summary>
[GlobalClass]
public abstract partial class ReleaseVerb : Resource
{
    [Export, RequiredExport] public InputAction TriggerAction { get; private set; } = null!;

    /// <summary>Capability gate: can this verb release the given held grabbable?</summary>
    public abstract bool CanRelease(IGrabbable3D grabbable);

    /// <summary>Perform the release. Implementations must release the item before the holder
    /// stops holding it (the stop reparents the body, triggering <c>_ExitTree</c> teardown).</summary>
    public abstract void Release(IHolder3D holder, IGrabbable3D grabbable);

    #region Test Helpers
#if TOOLS

    internal void SetTriggerActionForTest(InputAction action) => TriggerAction = action;

#endif
    #endregion
}
