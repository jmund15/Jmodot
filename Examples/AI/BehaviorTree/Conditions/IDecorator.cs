namespace Jmodot.Examples.AI.BehaviorTree.Conditions;

using Implementation.AI.BehaviorTree.Tasks;

public interface IDecorator
{
    void DecoratorEnter();
    void DecoratorPostProcess(BTaskStatus postProcStatus);
    void DecoratorOnChangeStatus(BTaskStatus newStatus);
    void DecoratorExit();
}
