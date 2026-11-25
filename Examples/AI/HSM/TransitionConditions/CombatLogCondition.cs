namespace Jmodot.Examples.AI.HSM.TransitionConditions;

using Core.Combat;
using Godot;
using Implementation.AI.BB;
using Implementation.Combat;
using Jmodot.Core.AI.BB;
using Jmodot.Core.AI.HSM;
using Jmodot.Implementation.Shared;

/// <summary>
/// Abstract base for any condition that checks the Combat Event Log.
/// Handles the safe retrieval of the log from the Blackboard.
/// </summary>
[Tool]
public abstract partial class CombatLogCondition : TransitionCondition
{
    public override bool Check(Node agent, IBlackboard bb)
    {
        // 1. Safe Retrieval
        // We use the constant key from the Integrator to ensure type safety.
        if (!bb.TryGet(BBDataSig.CombatLog, out CombatLog? log))
        {
            JmoLogger.Warning(this, "Couldn't find combat log!");
            // If there is no log, no combat events could have possibly happened.
            return false;
        }

        return CheckEvent(log!, bb);
    }

    /// <summary>
    /// Implemented by subclasses to query the specific event type. <paramref name="bb"/> is the checked entity's
    /// blackboard; a condition reads what it needs from it and keeps no per-evaluation state, because one Resource
    /// serves every entity that authors it.
    /// </summary>
    protected abstract bool CheckEvent(CombatLog log, IBlackboard bb);
}
