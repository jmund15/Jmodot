namespace Jmodot.Core.Components;

using AI.BB;

public interface IEntityComponentOwner
{
    bool InitializeDependenciesOfChildComponents(IBlackboard bb);
}
