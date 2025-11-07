namespace Jmodot.Core.Components;

using Core.AI.BB;
using Shared;

/// <summary>
/// Interface for components that require blackboard-based dependency initialization.
/// Implement this on any Node type to enable standardized dependency management.
/// </summary>
public interface IComponent : IGodotNodeInterface
{
    bool IsInitialized { get; }
    /// <summary>
    /// Override to retrieve specific dependencies from the blackboard.
    /// Document required BBDataSig keys in the class summary.
    /// Return false if any required dependency is missing.
    /// </summary>
    bool Initialize(IBlackboard bb);
    void OnPostInitialize();
}
