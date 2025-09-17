#region

using Jmodot.Core.AI.BB;

#endregion

namespace Jmodot.Implementation.AI.BehaviorTree.Utility;

[GlobalClass]
public abstract partial class ConsiderationModifier : Resource
{
    public abstract float Modify(float baseScore, IBlackboard blackboard);
}