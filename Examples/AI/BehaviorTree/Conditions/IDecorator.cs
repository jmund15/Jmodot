#region

using Jmodot.Implementation.AI.BehaviorTree.Tasks;

#endregion

namespace Jmodot.Examples.AI.BehaviorTree.Conditions;

public interface IDecorator
{
    public void DecoratorEnter();
    public void DecoratorPostProcess(BTaskStatus postProcStatus);
    public void DecoratorOnChangeStatus(BTaskStatus newStatus);
    public void DecoratorExit();
}