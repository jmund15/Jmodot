namespace Jmodot.Core.Components;

using Core.AI.BB;
using Shared;

/// <summary>
/// Interface for components that require blackboard-based dependency initialization.
/// Implement this on any Node type to enable standardized dependency management.
/// </summary>
public interface IEntityComponent : IGodotNodeInterface
{
    // TODO:
    //  - Add an initialized propert or check here
    //    We need to know in the component if it's been initialized yet, before it starts doing anything.

    // /// <summary>
    // /// The blackboard instance for this component's entity.
    // /// Set automatically during initialization.
    // /// </summary>
    // /// <remarks>Should only be set my the EntityComponentInitializer or similar.</remarks>
    // IBlackboard BB { get; } // set; } ?????

    /// <summary>
    /// Override to retrieve specific dependencies from the blackboard.
    /// Document required BBDataSig keys in the class summary.
    /// Return false if any required dependency is missing.
    /// </summary>
    bool InitializeDependencies(IBlackboard bb);
}
