namespace Jmodot.Examples.AI.BehaviorTree.Conditions;

using Implementation.AI.BehaviorTree.Tasks;

public interface IDecorator
{
    void DecoratorEnter();
    void DecoratorPostProcess(TaskStatus postProcStatus);
    void DecoratorOnChangeStatus(TaskStatus newStatus);
    void DecoratorExit();
}
