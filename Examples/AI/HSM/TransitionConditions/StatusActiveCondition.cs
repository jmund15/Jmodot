namespace Jmodot.Examples.AI.HSM.TransitionConditions;

using Godot;
using Implementation.AI.BB;
using Implementation.Combat;
using Jmodot.Core.AI.BB;
using Jmodot.Core.AI.HSM;
using Jmodot.Core.Combat;
using Jmodot.Core.Shared.Attributes;

/// <summary>
/// A transition condition that checks if a specific CombatTag is currently active
/// on the StatusEffectComponent. Used for state-based transitions like
/// "exit freeze when freeze tag is no longer active".
/// </summary>
[GlobalClass]
public partial class StatusActiveCondition : TransitionCondition
{
    /// <summary>
    /// The tag to check for on the StatusEffectComponent.
    /// </summary>
    [Export, RequiredExport] public CombatTag RequiredTag { get; set; } = null!;

    /// <summary>
    /// If true, inverts the result (returns true when tag is NOT active).
    /// Useful for exit conditions: "exit freeze when freeze is no longer active".
    /// </summary>
    [Export] public bool Inverted { get; set; } = false;

    public override bool Check(Node agent, IBlackboard bb)
    {
        if (!bb.TryGet<StatusEffectComponent>(BBDataSig.StatusEffects, out var statusComp))
        {
            return false;
        }

        if (statusComp == null || RequiredTag == null)
        {
            return false;
        }

        bool hasTag = statusComp.HasTag(RequiredTag);
        return Inverted ? !hasTag : hasTag;
    }
}
