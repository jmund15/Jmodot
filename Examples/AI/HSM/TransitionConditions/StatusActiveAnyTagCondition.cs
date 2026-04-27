namespace Jmodot.Examples.AI.HSM.TransitionConditions;

using Godot;
using Implementation.AI.BB;
using Implementation.Combat;
using Jmodot.Core.AI.BB;
using Jmodot.Core.AI.HSM;
using Jmodot.Core.Combat;
using GCol = Godot.Collections;

/// <summary>
/// Transition condition that returns true iff ANY tag in <see cref="Tags"/> is
/// currently active on the target's <see cref="StatusEffectComponent"/>. The
/// OR-mode counterpart to <see cref="StatusActiveCondition"/>.
/// </summary>
/// <remarks>
/// Used by <c>BehaviorSuppressedState</c> entry/exit transitions — author the
/// list of behavior-altering tags (freeze, stun, root, etc.) once on the entry
/// transition .tres and the same list with <see cref="Inverted"/>=true on the
/// exit transition. Adding a new behavior-altering status type means adding it
/// to those arrays plus a new ProfileMap entry on the entity, no new
/// transition class needed.
/// </remarks>
[GlobalClass]
public partial class StatusActiveAnyTagCondition : TransitionCondition
{
    /// <summary>
    /// Tags to OR together. Any one being active on the StatusEffectComponent makes Check return true.
    /// </summary>
    [Export] public GCol.Array<CombatTag> Tags { get; set; } = [];

    /// <summary>
    /// If true, returns true when NONE of the tags are active. Useful for exit transitions.
    /// Note: when the target has no <see cref="StatusEffectComponent"/> on its blackboard,
    /// Check returns false regardless of Inverted (missing prerequisites do not flip a transition).
    /// </summary>
    [Export] public bool Inverted { get; set; }

    public override bool Check(Node agent, IBlackboard bb)
    {
        if (!bb.TryGet<StatusEffectComponent>(BBDataSig.StatusEffects, out var statusComp) || statusComp == null)
        {
            // Convention: missing prerequisites do not flip a transition. Return
            // false regardless of Inverted — the transition simply does not fire.
            return false;
        }

        if (Tags == null || Tags.Count == 0)
        {
            return false;
        }

        bool anyActive = false;
        foreach (var tag in Tags)
        {
            if (tag != null && statusComp.HasTag(tag))
            {
                anyActive = true;
                break;
            }
        }

        return Inverted ? !anyActive : anyActive;
    }
}
