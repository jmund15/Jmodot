namespace Jmodot.Implementation.Interaction.Attachment;

using System;
using System.Collections.Generic;
using Godot;
using Jmodot.Implementation.Visual;

/// <summary>
/// Abstract base Resource for a host's rider-PLACEMENT rule: where on its silhouette an
/// <see cref="Core.Interaction.IAttachmentHost"/> seats the next rider. Authoring the rule as a
/// Resource keeps the derivation visible in the Inspector — spread, attempt budget and off-plane
/// depth are per-host art decisions, and compiling them into the placer hid them from the designer
/// entirely.
///
/// <para>
/// <b>The host owns the layout, not the rider.</b> How riders are arranged across a silhouette —
/// including how far off its plane they may sit — is a property of the host's art, so every knob
/// lives here rather than being answered once per rider.
/// </para>
///
/// <para>
/// <b>Stateless.</b> One <c>.tres</c> is shared across every host that authors it, so the occupied
/// anchors are passed in per call and nothing is retained between them.
/// </para>
///
/// <para>
/// <b>Subclass rule:</b> concrete subclasses MUST be marked <c>[GlobalClass, Tool]</c> — otherwise
/// <c>.tres</c> files deserialize as bare <see cref="Resource"/> and throw
/// <see cref="System.InvalidCastException"/> on type-checked access.
/// </para>
/// </summary>
[GlobalClass, Tool]
public abstract partial class AttachmentAnchorProfile3D : Resource
{
    /// <summary>
    /// An entity-local anchor for a rider of <paramref name="footprint"/>, or null when the host's
    /// art could not be measured or no candidate cleared <paramref name="occupied"/>.
    /// </summary>
    /// <param name="bounds">The host's measured silhouette. <see cref="VisualBounds3D.Unmeasured"/>
    /// when its art could not be measured — concretes must answer null rather than invent a layout.</param>
    /// <param name="occupied">Entity-local anchors already taken on this host.</param>
    /// <param name="footprint">How much capacity the incoming rider occupies.</param>
    /// <param name="nextUnitFloat">Roll source yielding values in [0, 1).</param>
    public abstract Vector3? Place(
        VisualBounds3D bounds,
        IReadOnlyList<Vector3> occupied,
        float footprint,
        Func<float> nextUnitFloat);
}
