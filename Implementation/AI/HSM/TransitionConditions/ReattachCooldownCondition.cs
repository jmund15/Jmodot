namespace Jmodot.Implementation.AI.HSM.TransitionConditions;

using Core.AI.BB;
using Core.AI.HSM;
using Core.Interaction;
using BB;

/// <summary>
/// Gates on whether a shed still bars this agent's rider from claiming a host again, by reading the
/// rider component's own <see cref="IAttachmentRider.IsReattachOnCooldown"/>.
/// </summary>
/// <remarks>
/// Sibling to <see cref="AttachmentRiderCondition"/> rather than a second export on it: "am I riding"
/// and "may I start riding" are independent axes, and an agent that is neither attached nor permitted
/// to attach is exactly the state a recoil has to route through. Without this the refusal is invisible
/// to the state machine — the attach action fails silently on its own cadence and the agent reads as
/// hung rather than as recovering.
/// </remarks>
[GlobalClass, Tool]
public partial class ReattachCooldownCondition : TransitionCondition
{
    /// <summary>True fires this transition while the rider is barred from reattaching; false while it is free to.</summary>
    [Export] public bool RequiredIsOnCooldown { get; private set; }

    public override bool Check(Node agent, IBlackboard bb)
    {
        if (!bb.TryGet<IAttachmentRider>(BBDataSig.AttachmentRider, out var rider) || rider == null)
        {
            // No rider is never barred, so an absent one answers the free-to-attach question
            // affirmatively — matching the sibling condition's treatment of the same absence.
            return !this.RequiredIsOnCooldown;
        }

        return rider.IsReattachOnCooldown == this.RequiredIsOnCooldown;
    }
}
