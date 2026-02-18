namespace Jmodot.Core.Components;

using System;
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

    /// <summary>
    /// Fired when the component has been successfully initialized.
    /// Implementors MUST initialize this event with <c>= delegate { };</c>
    /// to guarantee null-safe invocation without conditional checks.
    /// </summary>
    event Action Initialized;
}
