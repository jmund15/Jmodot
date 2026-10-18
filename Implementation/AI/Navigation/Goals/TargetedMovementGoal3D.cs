namespace Jmodot.Implementation.AI.Navigation.Goals;

using System;
using System.Collections.Generic;
using Core.AI.BB;
using Core.AI.Navigation.Considerations;
using Jmodot.Implementation.AI.Navigation.Targets;
using Jmodot.Implementation.Shared;
using Godot;

/// <summary>
/// A goal whose objective is about one authored target. An empty Target follows the entity's
/// primary target with no key to type; assign a TrackedTarget3D to follow another key.
/// </summary>
[GlobalClass, Tool]
public abstract partial class TargetedMovementGoal3D : MovementGoal3D
{
    /// <summary>
    /// What this goal moves relative to. Empty follows the primary target, so the common case needs
    /// no resource and no key. Assign a TrackedTarget3D to follow a different memory.
    /// </summary>
    [Export] public MovementTarget3D? Target { get; private set; }

    /// <summary>True when this goal's objective can hold a whole set of members.</summary>
    protected virtual bool AcceptsSetTarget => false;

    public override AIConsiderationRuntime? CreateRuntime(IBlackboard? bb) => new TargetRuntime();

    /// <summary>
    /// Resolves the target into the runtime's sample and returns it. A set target on a goal that
    /// steers at one member fails closed here, before the goal reads Point, and says so once.
    /// </summary>
    protected TargetSample ResolveTarget(in MovementGoalContext ctx, AIConsiderationRuntime? runtime)
    {
        var sample = (runtime as TargetRuntime)?.Sample ?? new TargetSample();
        MovementTarget3D.ResolveOrPrimary(Target, ctx.AgentPosition, ctx.BB, ctx.Memory, sample);
        if (AcceptsSetTarget || sample.Geometry != TargetSampleGeometry.Set) { return sample; }

        sample.State = TargetSampleState.Unavailable;
        if (sample.TryBeginLog(TargetLogKind.ConfigError))
        {
            JmoLogger.Error(this,
                $"Goal '{ResourceName}' ({GetType().Name}) steers at one member, but its target resolves " +
                "a set. Assign a NearestMember policy or a tracked target to this goal.");
        }
        return sample;
    }

    public override string[] GetAgentWarnings(Node agent)
        => Target?.GetAgentWarnings(agent) ?? Array.Empty<string>();

    public override string[] GetResourceConfigurationWarnings()
    {
        var warnings = new List<string>();
        warnings.AddRange(ConfigWarnings.ResourceSlotWarnings("Target", Target, required: false));
        if (!AcceptsSetTarget && Target?.Geometry == TargetSampleGeometry.Set)
        {
            warnings.Add("Target resolves a set of members, but this goal steers at one member. " +
                         "Assign a NearestMember policy or a tracked target.");
        }
        return warnings.ToArray();
    }

    #region Test Helpers
#if TOOLS
    internal void SetTargetForTest(MovementTarget3D? target) => Target = target;
#endif
    #endregion
}
