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
public abstract partial class CombatLogCondition : TransitionCondition
{
    /// <summary>
    /// The agent node currently being checked. Available during CheckEvent calls.
    /// </summary>
    protected Node? CurrentAgent { get; private set; }

    public override bool Check(Node agent, IBlackboard bb)
    {
        CurrentAgent = agent;

        // 1. Safe Retrieval
        // We use the constant key from the Integrator to ensure type safety.
        if (!bb.TryGet(BBDataSig.CombatLog, out CombatLog? log))
        {
            JmoLogger.Warning(this, "Couldn't find combat log!");
            // If there is no log, no combat events could have possibly happened.
            return false;
        }

        return CheckEvent(log!);
    }

    /// <summary>
    /// Implemented by subclasses to query the specific event type.
    /// </summary>
    protected abstract bool CheckEvent(CombatLog log);
}
