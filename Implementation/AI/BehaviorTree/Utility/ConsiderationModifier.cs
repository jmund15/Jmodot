namespace Jmodot.Implementation.AI.BehaviorTree.Utility;

using Core.AI.BB;

[GlobalClass]
public abstract partial class ConsiderationModifier : Resource
{
    public abstract float Modify(float baseScore, IBlackboard blackboard);
}
