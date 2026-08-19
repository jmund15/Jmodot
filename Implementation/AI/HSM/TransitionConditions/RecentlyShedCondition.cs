namespace Jmodot.Implementation.AI.HSM.TransitionConditions;

using Core.AI.BB;
using Core.AI.HSM;
using Core.Interaction;
using BB;

/// <summary>
/// Fires while this agent's rider was shed within the last <see cref="WithinSeconds"/>.
/// </summary>
/// <remarks>
/// The entry route into a launch or recovery state for an entity thrown off a host, and it reads the
/// shed EVENT rather than the impulse the shed produced. That distinction is the whole point: an
/// entity that was thrown off has been thrown off whatever the force, so routing the launch off a
/// knockback magnitude makes the fling-strength dial secretly double as an on/off switch for the
/// entire launch behaviour, with no signal when a tuning pass crosses the line. Force decides how
/// far; this decides whether.
/// <para>
/// Pair it with a magnitude-gated transition to the same state rather than replacing one with the
/// other — a hard hit and a shed are independent causes of the same response, and each wants its own
/// condition.
/// </para>
/// </remarks>
[GlobalClass, Tool]
public partial class RecentlyShedCondition : TransitionCondition
{
    /// <summary>
    /// How recently the shed must have happened. Sized to cover the frames between the shed and the
    /// transition being evaluated, not to time the flight — the state's own exit owns that.
    /// </summary>
    [Export(PropertyHint.Range, "0.05,2.0,0.05,suffix:s")]
    public float WithinSeconds { get; private set; } = 0.3f;

    public override bool Check(Node agent, IBlackboard bb)
    {
        if (!bb.TryGet<IAttachmentRider>(BBDataSig.AttachmentRider, out var rider) || rider == null)
        {
            // No rider has never been shed. Non-riding agents can carry this transition harmlessly.
            return false;
        }

        return rider.SecondsSinceShed <= this.WithinSeconds;
    }
}
