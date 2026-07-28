namespace Jmodot.Implementation.Components;

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
public class EntityNodeComponentsInitializer
{
    /// <summary>
    /// Publishes every <see cref="IBlackboardProvider"/> descendant's provision onto
    /// <paramref name="bb"/>. Scanned independently of <see cref="IComponent"/> so passive
    /// publishers (nodes that implement only <c>Provision</c>) participate. Exposed for
    /// bootstrap paths that run their own Phase 1 but still need the Phase-0 barrier.
    /// </summary>
    public static void RunPhase0(Node entity, IBlackboard bb)
    {
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

            bb.Set(key, value); // This can be overwritten by the Entity's manual setup
        }
    }

    public List<IComponent> InitializeComponents(Node entity, IBlackboard bb)
    {
        var components = entity.GetChildrenOfInterface<IComponent>().ToList();
        List<IComponent> initializedComps = new();

        RunPhase0(entity, bb);

        // Phase 1: Initialize Dependencies
        foreach (var component in components)
        {
            if (component.Initialize(bb))
            {
                initializedComps.Add(component);
            }
            else
            {
                JmoLogger.Error(component, "Failed to initialize required dependencies. Component will not be activated.");
            }
        }

        // Phase 2: Post-Initialize — event subscriptions and activation.
        foreach (var component in initializedComps)
        {
            component.OnPostInitialize();
        }

        return initializedComps;
    }
}
