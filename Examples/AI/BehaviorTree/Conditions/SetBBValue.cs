namespace Jmodot.Examples.AI.BehaviorTree.Conditions;

using Core.AI.BB;
using Implementation.AI.BB;

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
        this._valueToSet = BBDataSig.Agent;
        this._value = default;
    }

    #endregion

    #region TASK_UPDATES

    public override void Init(Node agent, IBlackboard bb)
    {
        base.Init(agent, bb);
        this.ConditionName = $"_Set{this._valueToSet}To{this._value}";
    }

    public override void Enter()
    {
        base.Enter();
        this.BB.SetPrimVar(this._valueToSet, this._value);
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
