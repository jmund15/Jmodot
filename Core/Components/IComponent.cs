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
/// <para>
/// Every phase may run more than once on the same instance — spell scenes run Phase 0 at
/// <c>_Ready</c> and the whole chain again at Initialize, and pool reuse re-runs it per
/// activation. <c>IBlackboardProvider.Provision</c>, <see cref="Initialize"/> and
/// <see cref="OnPostInitialize"/> must therefore all be idempotent. Prefer tearing down prior
/// subscriptions at the top of <see cref="Initialize"/>; a re-entrancy flag reset by the same
/// teardown path is the fallback where teardown-first is invasive.
/// </para>
/// <para>
/// Returning false from <see cref="Initialize"/> retracts the component's Phase-0 provision from
/// the blackboard and disables its <c>_Process</c>/<c>_PhysicsProcess</c>. A rejected component
/// must not stay resolvable, or siblings subscribe to events it can never raise.
/// </para>
/// <para>
/// <c>[Export]</c>-resolved dependencies are exempt from the Phase-2 subscription rule: a
/// scene-load-time reference is not subject to the sibling-ordering race, so subscribing to it in
/// <c>_Ready</c> is correct (canonical: <c>VisualEffectController.Composer</c>).
/// </para>
/// </remarks>
public interface IComponent : IGodotNodeInterface
{
    bool IsInitialized { get; }
    /// <summary>
    /// Resolve dependencies from the blackboard. Document required BBDataSig keys in the
    /// class summary. Return false if a dependency without which the component serves no
    /// purpose is missing — the initializer logs the Error, retracts this component's provision,
    /// disables its processing, and omits it. The component's own log line for a missing key is a
    /// Debug-level detail note, not a second Error.
    /// Sibling components are resolvable here, but their events are NOT yet safe to
    /// subscribe to: siblings later in scene order have not been initialized. Subscribe in
    /// <see cref="OnPostInitialize"/> instead.
    /// </summary>
    bool Initialize(IBlackboard bb);

    /// <summary>
    /// Runs after EVERY sibling has completed <see cref="Initialize"/>. This is the home
    /// for sibling-event subscriptions, signal connections, and process activation.
    /// MUST be safe to run more than once on the same instance — the initializer calls it
    /// unconditionally for every component that returned true, and a second entity-init pass
    /// (pool reuse, scene rebind) re-runs both phases.
    /// </summary>
    void OnPostInitialize();

    /// <summary>
    /// Fired when the component has been successfully initialized.
    /// Implementors MUST initialize this event with <c>= delegate { };</c>
    /// to guarantee null-safe invocation without conditional checks.
    /// </summary>
    event Action Initialized;
}
