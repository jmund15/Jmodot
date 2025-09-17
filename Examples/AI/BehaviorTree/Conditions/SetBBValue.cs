#region

using Jmodot.Core.AI.BB;
using Jmodot.Implementation.AI.BB;

#endregion

namespace Jmodot.Examples.AI.BehaviorTree.Conditions;

[GlobalClass]
[Tool]
public partial class SetBBValue : BTCondition
{
    #region TASK_VARIABLES

    [Export] private string _valueToSet; //BBDataSig

    [Export]
    //private Variant _value;
    private Variant _value; //TODO: WHEN GODOT SUPPORT VARIANT EXPORT ADD BACK

    public SetBBValue()
    {
        _valueToSet = BBDataSig.Agent;
        _value = default;
    }

    #endregion

    #region TASK_UPDATES

    public override void Init(Node agent, IBlackboard bb)
    {
        base.Init(agent, bb);
        ConditionName = $"_Set{_valueToSet}To{_value}";
    }

    public override void Enter()
    {
        base.Enter();
        BB.SetPrimVar(_valueToSet, _value);
    }

    public override void Exit()
    {
        base.Exit();
    }

    public override void ProcessFrame(float delta)
    {
        base.ProcessFrame(delta);
    }

    public override void ProcessPhysics(float delta)
    {
        base.ProcessPhysics(delta);
    }

    #endregion
}