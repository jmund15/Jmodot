namespace Jmodot.Implementation.AI.HSM.TransitionConditions;

using Core.AI.BB;
using Core.AI.HSM;
using Core.Interaction;
using BB;

/// <summary>
/// Routes an entity in and out of its riding state by reading the rider component's own
/// <see cref="IAttachmentRider.IsAttached"/>, rather than a blackboard flag mirroring it. One writer,
/// so there is no frame in which the flag and the attachment disagree.
/// </summary>
/// <remarks>
/// An absent rider is treated as NOT attached, which is deliberately asymmetric: it lets the exit-side
/// transition fire, so an entity whose rider was never provisioned (or was retracted) cannot strand in a
/// ride nothing is driving. The loud signal for that misconfiguration is the component initializer's
/// hard-dependency error, not a wedged state machine.
/// </remarks>
[GlobalClass, Tool]
public partial class AttachmentRiderCondition : TransitionCondition
{
    /// <summary>True fires this transition while the rider is riding; false while it is not.</summary>
    [Export] public bool RequiredIsAttached { get; private set; }

    public override bool Check(Node agent, IBlackboard bb)
    {
        if (!bb.TryGet<IAttachmentRider>(BBDataSig.AttachmentRider, out var rider) || rider == null)
        {
            return !this.RequiredIsAttached;
        }

        return rider.IsAttached == this.RequiredIsAttached;
    }
}
