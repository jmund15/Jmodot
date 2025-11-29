using Godot;
using Jmodot.Core.Combat;
using Jmodot.Implementation.Combat;
using Jmodot.Implementation.AI.BB;

namespace Jmodot.Implementation.Combat.Status;

public class StunStatus : TimedStatus
{
    private const string STUN_KEY = "IsStunned";

    public StunStatus(float duration, Node source, ICombatant target) 
        : base(duration, source, target)
    {
    }

    public override void OnApply()
    {
        base.OnApply();
        // Set the blackboard key
        if (Target.Blackboard != null)
        {
            Target.Blackboard.Set(STUN_KEY, true);
        }
    }

    public override void OnRemove()
    {
        base.OnRemove();
        
        // Check if any other stuns remain
        // We need to get the StatusComponent from the Blackboard
        if (Target.Blackboard.TryGet(BBDataSig.StatusEffects, out StatusEffectComponent statusComp))
        {
            if (!statusComp.HasStatus<StunStatus>())
            {
                Target.Blackboard.Set(STUN_KEY, false);
            }
        }
        else
        {
            // Fallback if component is missing (shouldn't happen if set up correctly)
             Target.Blackboard.Set(STUN_KEY, false);
        }
    }
}
