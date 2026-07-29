namespace Jmodot.Implementation.Components;

using System;
using System.Collections.Generic;
using System.Linq;
using Core.AI.BB;
using Core.Components;
using Shared;

/// <summary>
/// Runs the three-phase entity component contract over a node's descendants:
/// Phase 0 publishes every <see cref="IBlackboardProvider"/> provision, Phase 1 calls
/// <see cref="IComponent.Initialize"/> on every component, Phase 2 calls
/// <see cref="IComponent.OnPostInitialize"/> on the ones that succeeded. The phase
/// barriers are what let a component resolve any sibling in Phase 1 and subscribe to
/// any sibling's events in Phase 2 without caring about scene order.
/// </summary>
public static class EntityNodeComponentsInitializer
{
    /// <summary>
    /// Publishes every <see cref="IBlackboardProvider"/> descendant's provision onto
    /// <paramref name="bb"/>. Scanned independently of <see cref="IComponent"/> so passive
    /// publishers (nodes that implement only <c>Provision</c>) participate. Exposed for
    /// bootstrap paths that run their own Phase 1 but still need the Phase-0 barrier.
    /// </summary>
    public static void RunPhase0(Node entity, IBlackboard bb)
    {
        var publishedThisPass = new HashSet<StringName>();

        foreach (var provider in entity.GetChildrenOfInterface<IBlackboardProvider>())
        {
            var provision = provider.Provision;
            if (!provision.HasValue)
            {
                continue;
            }

            var (key, value) = provision.Value;
            if (key == null || value == null)
            {
                continue;
            }

            // Last writer wins, so two publishers on one key make the surviving value depend on
            // child order — a scene-authoring error that is otherwise completely silent.
            if (!publishedThisPass.Add(key))
            {
                JmoLogger.Warning(entity,
                    $"Duplicate Phase-0 provision for key '{key}': more than one provider publishes it, "
                    + "so which one consumers resolve depends on child order.");
            }

            // Phase 0 runs first inside InitializeComponents, so a provision written here WINS over
            // any manual bb.Set the entity root performed before calling in. Roots that need to
            // override a provision must set the key after InitializeComponents returns.
            bb.Set(key, value);
        }
    }

    public static List<IComponent> InitializeComponents(Node entity, IBlackboard bb)
    {
        var components = entity.GetChildrenOfInterface<IComponent>().ToList();
        List<IComponent> initializedComps = new();
        List<IComponent> failedComps = new();

        RunPhase0(entity, bb);

        // Phase 1: Initialize Dependencies. Each component is isolated: this runs inside the entity
        // root's _Ready, where Godot swallows an unhandled throw with no log line, so one bad
        // component would otherwise leave every later sibling uninitialized and invisible.
        foreach (var component in components)
        {
            bool resolved;
            try
            {
                resolved = component.Initialize(bb);
            }
            catch (Exception ex)
            {
                JmoLogger.Error(component, $"Threw during Initialize: {ex}");
                resolved = false;
            }

            if (resolved)
            {
                initializedComps.Add(component);
                continue;
            }

            failedComps.Add(component);

            // "Not activated" has to be enforced, not merely announced. SetProcess rather than
            // ProcessMode.Disabled so a failed Area-derived component still reports overlaps.
            var node = component.GetUnderlyingNode();
            node.SetProcess(false);
            node.SetPhysicsProcess(false);
            JmoLogger.Error(component, "Failed to initialize required dependencies. Component processing disabled.");
        }

        RetractFailedProvisions(bb, failedComps);

        // Phase 2: Post-Initialize — event subscriptions and activation. Isolated per component for
        // the same reason as Phase 1: a throw here would abort the loop and leave every later
        // sibling unwired.
        foreach (var component in initializedComps)
        {
            try
            {
                component.OnPostInitialize();
            }
            catch (Exception ex)
            {
                JmoLogger.Error(component, $"Threw during OnPostInitialize — component left unsubscribed: {ex}");
            }
        }

        return initializedComps;
    }

    /// <summary>
    /// Phase 0 publishes unconditionally, so a component that fails Phase 1 leaves its self-reference
    /// resolvable by every sibling — turning one loud failure into a silent multi-component one
    /// (consumers resolve a half-dead component and subscribe to events it can never raise).
    /// Runs after the whole Phase-1 pass so a failure cannot retract a key a later sibling
    /// legitimately republished.
    /// </summary>
    private static void RetractFailedProvisions(IBlackboard bb, List<IComponent> failedComps)
    {
        foreach (var component in failedComps)
        {
            if (component is not IBlackboardProvider provider)
            {
                continue;
            }

            var provision = provider.Provision;
            if (!provision.HasValue || provision.Value.Key == null)
            {
                continue;
            }

            var key = provision.Value.Key;
            if (!bb.TryGet<object>(key, out var current) || !ReferenceEquals(current, provision.Value.Value))
            {
                continue;
            }

            bb.Set<object?>(key, null);
            JmoLogger.Error(component,
                $"Retracted Phase-0 provision '{key}': its publisher failed to initialize, so the key "
                + "is no longer resolvable. Any component that depends on it will fail too.");
        }
    }
}
