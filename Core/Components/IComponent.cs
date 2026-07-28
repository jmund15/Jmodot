namespace Jmodot.Core.Components;

using System;
using Core.AI.BB;
using Shared;

/// <summary>
/// Interface for components that require blackboard-based dependency initialization.
/// Implement this on any Node type to enable standardized dependency management.
/// </summary>
/// <remarks>
/// <c>EntityNodeComponentsInitializer</c> drives three barriered phases over an entity:
/// Phase 0 publishes every <c>IBlackboardProvider</c> provision, Phase 1 calls
/// <see cref="Initialize"/> on every component, Phase 2 calls
/// <see cref="OnPostInitialize"/> on the ones that returned true. Implementors must NOT
/// self-call <see cref="OnPostInitialize"/> from <see cref="Initialize"/> — doing so
/// re-opens the sibling-ordering race the barriers exist to close.
/// </remarks>
public interface IComponent : IGodotNodeInterface
{
    bool IsInitialized { get; }
    /// <summary>
    /// Resolve dependencies from the blackboard. Document required BBDataSig keys in the
    /// class summary. Return false if a dependency without which the component serves no
    /// purpose is missing — the initializer logs an Error and omits the component.
    /// Sibling components are resolvable here, but their events are NOT yet safe to
    /// subscribe to: siblings later in scene order have not been initialized. Subscribe in
    /// <see cref="OnPostInitialize"/> instead.
    /// </summary>
    bool Initialize(IBlackboard bb);

    /// <summary>
    /// Runs after EVERY sibling has completed <see cref="Initialize"/>. This is the home
    /// for sibling-event subscriptions, signal connections, and process activation.
    /// </summary>
    void OnPostInitialize();

    /// <summary>
    /// Fired when the component has been successfully initialized.
    /// Implementors MUST initialize this event with <c>= delegate { };</c>
    /// to guarantee null-safe invocation without conditional checks.
    /// </summary>
    event Action Initialized;
}
