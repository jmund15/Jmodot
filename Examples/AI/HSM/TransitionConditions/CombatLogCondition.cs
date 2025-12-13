namespace Jmodot.Examples.AI.HSM.TransitionConditions;

using Core.Combat;
using Godot;
using Implementation.Combat;
using Jmodot.Core.AI.BB;
using Jmodot.Core.AI.HSM;

/// <summary>
/// Abstract base for any condition that checks the Combat Event Log.
/// Handles the safe retrieval of the log from the Blackboard.
/// </summary>
public abstract partial class CombatLogCondition : TransitionCondition
{
    public override bool Check(Node agent, IBlackboard bb)
    {
        // 1. Safe Retrieval
        // We use the constant key from the Integrator to ensure type safety.
        if (!bb.TryGet(CombatLogger.BB_CombatLog, out CombatLog log))
        {
            // If there is no log, no combat events could have possibly happened.
            return false;
        }

        return CheckEvent(log);
    }

    /// <summary>
    /// Implemented by subclasses to query the specific event type.
    /// </summary>
    protected abstract bool CheckEvent(CombatLog log);
}
