namespace Jmodot.Examples.AI.BehaviorTree.Conditions;

using Core.AI.BB;
using Core.AI.BehaviorTree.Conditions;
using Core.Shared.Attributes;
using Godot;
using Implementation.AI.BehaviorTree.Tasks;

/// <summary>
/// Gates its task on a <see cref="CooldownChannel"/> being elapsed. Stateless by construction —
/// the timestamp lives on the agent's blackboard — so unlike <see cref="CooldownCondition"/> it
/// survives tree re-inits, condition re-clones, and any non-Success exit of its parent task.
/// </summary>
/// <remarks>
/// This condition never arms the cooldown; the system that performs the gated event calls
/// <see cref="CooldownChannel.Arm"/> on the SAME authored channel resource.
/// </remarks>
[GlobalClass, Tool]
public partial class BBCooldownReadyCondition : BTCondition
{
    /// <summary>The cooldown this task waits on. Required — an unset channel must fail loud, never read as permanently ready.</summary>
    [Export, RequiredExport] public CooldownChannel Channel { get; private set; } = null!;

    public override void Init(BehaviorTask owner, Node agent, IBlackboard bb)
    {
        base.Init(owner, agent, bb);
        this.ValidateRequiredExports();
    }

    /// <summary>
    /// Whether the channel is elapsed on the agent's blackboard. Callers must run
    /// <see cref="Init"/> first — the blackboard this reads arrives there.
    /// </summary>
    public override bool Check() => this.Channel.IsReady(this.BB);

    #region Test Helpers
#if TOOLS
    internal void SetChannelForTest(CooldownChannel value) => this.Channel = value;
#endif
    #endregion
}
