namespace Jmodot.Implementation.Components;

using Core.Components;
using Core.AI.BB;
using Implementation.Shared;

/// <summary>
/// Provides standardized blackboard initialization for entity components.
/// Single reusable class - no need for derived implementations.
/// </summary>
public class EntityComponentInitializer
{
    private readonly Node _component;
    private readonly IEntityComponent _entityComponent;

    public IBlackboard BB { get; private set; } = null!;
    public Node Owner { get; private set; } = null!;
    public bool IsInitialized { get; private set; }

    public EntityComponentInitializer(Node component, IEntityComponent entityComponent)
    {
        _component = component;
        _entityComponent = entityComponent;
    }

    /// <summary>
    /// Performs standardized component initialization.
    /// Call this in your component's _Ready() method.
    /// Returns true if successful, false otherwise.
    /// </summary>
    public bool Initialize()
    {
        Owner = _component.GetOwner();
        if (Owner == null)
        {
            JmoLogger.Error(_component, $"{_component.GetType().Name} requires an owner node");
            DisableProcessing();
            return false;
        }

        BB = Owner.GetFirstChildOfInterface<IBlackboard>();
        if (BB == null)
        {
            JmoLogger.Error(_component, $"Owner '{Owner.Name}' must have a Blackboard child");
            DisableProcessing();
            return false;
        }

        // // Set the blackboard on the component so it can access it in InitializeDependencies
        // _entityComponent.BB = BB;

        // Call the component's specific dependency initialization
        if (!_entityComponent.InitializeDependencies(BB))
        {
            JmoLogger.Error(_component, "Failed to initialize required dependencies from Blackboard");
            DisableProcessing();
            return false;
        }

        IsInitialized = true;
        return true;
    }

    private void DisableProcessing()
    {
        _component.SetProcess(false);
        _component.SetPhysicsProcess(false);

        if (_component is Node2D node2D)
        {
            node2D.SetProcessInput(false);
            node2D.SetProcessUnhandledInput(false);
        }
        else if (_component is Node3D node3D)
        {
            node3D.SetProcessInput(false);
            node3D.SetProcessUnhandledInput(false);
        }
    }
}
