namespace Jmodot.Implementation.Interaction.Attachment;

using Godot;
using Jmodot.Core.Stats;
using Jmodot.Implementation.Visual;

/// <summary>
/// Abstract base Resource for a host's rider-capacity rule: how much summed rider footprint an
/// <see cref="Core.Interaction.IAttachmentHost"/> can carry at once. Authoring the rule as a
/// Resource keeps every derivation visible in the Inspector — the alternative (a nullable override
/// falling back to a density constant) hides which knob is live and leaves the other one dead.
///
/// <para>
/// <b>Sibling family, not a subclass.</b> <see cref="Jmodot.Implementation.Combat.CapacityProviders.HitboxCapacityProvider3D"/> answers a discrete
/// "can I accept one more hit?" against a hit COUNT; this answers a continuous footprint BUDGET.
/// Neither contract expresses the other, so they share a shape and nothing else.
/// </para>
///
/// <para>
/// <b>Stateless.</b> One <c>.tres</c> is shared across every host that authors it, so the live
/// grip/capacity accounting lives on the host component, never here.
/// </para>
///
/// <para>
/// <b>Subclass rule:</b> concrete subclasses MUST be marked <c>[GlobalClass, Tool]</c> — otherwise
/// <c>.tres</c> files deserialize as bare <see cref="Resource"/> and throw
/// <see cref="System.InvalidCastException"/> on type-checked access.
/// </para>
/// </summary>
[GlobalClass, Tool]
public abstract partial class AttachmentCapacityProvider3D : Resource
{
    /// <summary>
    /// Total rider footprint <paramref name="owner"/> can carry right now.
    /// </summary>
    /// <param name="bounds">The host's measured silhouette. <see cref="VisualBounds3D.Unmeasured"/>
    /// when its art could not be measured — concretes that read it must answer 0 rather than invent one.</param>
    /// <param name="stats">The host's stat provider, or null on a host with no stats.</param>
    /// <param name="owner">The host entity, for concretes that need owner-side state.</param>
    public abstract float GetCapacity(VisualBounds3D bounds, IStatProvider? stats, Node? owner);
}
