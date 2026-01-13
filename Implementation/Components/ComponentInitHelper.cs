namespace Jmodot.Implementation.Components;

using Core.Components;
using Core.Shared;
using Jmodot.Core.AI.BB;

public class ComponentInitHelper : IGodotNodeInterface
{
    private readonly Node _owner;
    private readonly IComponent _comp;
    public bool IsInitialized { get; private set; } = false;

    public ComponentInitHelper(Node owner, IComponent comp)
    {
        _owner = owner;
        _comp = comp;

        _owner.ProcessMode = Node.ProcessModeEnum.Disabled;
    }

    public bool InitializeDependencies(IBlackboard bb)
    {
        if (IsInitialized) { return true; }
        if (!_comp.Initialize(bb)) { return false; }

        IsInitialized = true;
        return true;
    }

    public void OnPostInitialize()
    {
        if (!IsInitialized) { return; }
        _owner.ProcessMode = Node.ProcessModeEnum.Inherit;
        _comp.OnPostInitialize();
    }

    public Node GetUnderlyingNode()
    {
        return _owner;
    }
}
