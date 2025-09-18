namespace Jmodot.Examples.AI.BehaviorTree.Conditions;

using Core.AI.BB;
using Core.AI.BehaviorTree.Conditions;
using Implementation.AI.BB;

[GlobalClass]
[Tool]
public partial class CheckBBValue : BTCondition
{
    [Export] private string _key; //BBDataSig

    [Export]
    private Variant _value;

    private bool _checkResult;

    public CheckBBValue()
    {
    }
    public override void OnParentTaskEnter()
    {
        // try to fix

        // var bbVal = BB.GetPrimVar<Variant>(_key);
        // if (bbVal.HasValue() && bbVal.Value == _value)
        // {
        //
        // }

        _checkResult = false;
    }
    public override bool Check()
    {
        return _checkResult;
    }
}
