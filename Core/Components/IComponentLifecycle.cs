namespace Jmodot.Core.Components;

public interface IComponentLifecycle
{
    bool IsInitialized { get; }
    void OnPostInitialize();
}
