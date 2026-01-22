namespace Jmodot.Implementation.Components;

using System.Collections.Generic;
using System.Linq;
using Core.AI.BB;
using Core.Components;
using Shared;

public class EntityNodeComponentsInitializer
{
    public List<IComponent> InitializeComponents(Node entity, IBlackboard bb)
    {
        var components = entity.GetChildrenOfInterface<IComponent>().ToList();
        List<IComponent> initializedComps = new();

        // --- NEW: PHASE 0: AUTO-REGISTRATION ---
        //JmoLogger.Info(entity, "Starting blackboard auto-registration pass...");
        foreach (var component in components)
        {
            if (component is IBlackboardProvider provider)
            {
                var provision = provider.Provision;
                if (provision.HasValue)
                {
                    var (key, value) = provision.Value;
                    if (key != null && value != null)
                    {
                        //JmoLogger.Info(entity, $"Component '{component.GetType().Name}' is providing '{key}' to the blackboard for object '{value}'.");
                        bb.Set(key, value); // This can be overwritten by the Entity's manual setup
                    }
                }
            }
        }

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

        // CURRENTLY DOING THIS IN THE COMPONENT ITSELF, leaving here in case we want to do it differently later.
        //
        // // Phase 2: Post-Initialize and Activation
        // foreach (var component in initializedComps)
        // {
        //     component.OnPostInitialize(); // For event subscriptions, etc.
        //
        //     // ACTIVATE IT!
        //     var componentNode = component.GetUnderlyingNode();
        //     componentNode.ProcessMode = Node.ProcessModeEnum.Inherit;
        //     JmoLogger.Info(component, "Component activated.");
        // }

        return initializedComps;
    }
}
