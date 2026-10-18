namespace Jmodot.Implementation.AI.Navigation.Goals;

using System.Collections.Generic;
using Core.AI.BB;
using Core.AI.Navigation.Considerations;
using Core.Movement;
using Core.Shared;
using Godot;
using Jmodot.AI.Navigation;
using Implementation.AI.Navigation;
using Implementation.AI.Perception;

/// <summary>
/// The outcome of one goal advance. A goal owns its own completion: it reports Succeeded or
/// Failed when its objective is met or impossible, and Running otherwise.
/// </summary>
public enum GoalStatus
{
    Running,
    Succeeded,
    Failed,
}

/// <summary>The world state a goal advances against, captured once per active physics tick.</summary>
public readonly struct MovementGoalContext
{
    public readonly Vector3 AgentPosition;
    public readonly IBlackboard BB;
    public readonly AIPerceptionManager3D? Memory;
    public readonly float Delta;

    public MovementGoalContext(Vector3 agentPosition, IBlackboard bb,
        AIPerceptionManager3D? memory, float delta)
    {
        AgentPosition = agentPosition;
        BB = bb;
        Memory = memory;
        Delta = delta;
    }
}

/// <summary>
/// Where a movement step goes and when it is done. A goal is a plain authored Resource shared by
/// every agent that runs it: it owns no per-agent state, and anything that varies per agent lives
/// on the <see cref="AIConsiderationRuntime"/> returned by <see cref="CreateRuntime"/>.
/// <para>
/// A goal is not a steering consideration. It may also steer, and <see cref="Steers"/> says so, but
/// considerations stay style and environment modifiers; they never decide completion.
/// </para>
/// </summary>
[GlobalClass, Tool]
public abstract partial class MovementGoal3D : Resource, IResourceConfigurationWarnings
{
    /// <summary>How this step drives movement while it runs; non-Full modes claim the steering
    /// processor for their duration and release it on exit.</summary>
    public virtual SteeringControlMode ControlMode => SteeringControlMode.Full;

    /// <summary>
    /// True when the goal reports Succeeded or Failed on its own. A goal that never ends itself
    /// needs a time limit or a later BT sibling to end its step.
    /// </summary>
    public virtual bool EndsItself => true;

    /// <summary>The distance geometry this goal acts in, or null when it has none.</summary>
    public virtual GoalDistanceBand? DistanceBand => null;

    /// <summary>True when this goal contributes its own steering direction while it runs.</summary>
    public virtual bool Steers => false;

    /// <summary>How loudly a steering goal competes with style terms; 0 keeps it registered and pulls nothing.</summary>
    public virtual float SteeringWeight => 0f;

    /// <summary>
    /// Faults that need the entity a goal is mounted on, such as a missing perception manager.
    /// The host renders them under the goal slot, so no host tests a goal type.
    /// </summary>
    public virtual string[] GetAgentWarnings(Node agent) => System.Array.Empty<string>();

    /// <summary>Creates this goal's per-agent state, called once per action node by the host.</summary>
    public virtual AIConsiderationRuntime? CreateRuntime(IBlackboard? bb) => null;

    /// <summary>Starts an activation. Called once when the step enters, before any advance.</summary>
    public virtual void OnEnter(in MovementGoalContext ctx, AIConsiderationRuntime? runtime) { }

    /// <summary>Advances the goal once per active physics tick and reports whether it ended.</summary>
    public abstract GoalStatus Advance(in MovementGoalContext ctx, AIConsiderationRuntime? runtime);

    /// <summary>Ends an activation. Called once when the step exits, including after a failure.</summary>
    public virtual void OnExit(in MovementGoalContext ctx, AIConsiderationRuntime? runtime) { }

    /// <summary>
    /// This goal's steering contribution for one evaluation. Reads the sample the last
    /// <see cref="Advance"/> resolved and no clock, so repeated calls in one tick return the same map.
    /// </summary>
    public virtual Dictionary<Vector3, float> Score(DirectionSet3D directions,
        SteeringDecisionContext3D ctx, IBlackboard bb, AIConsiderationRuntime? runtime)
        => new();

    public virtual string[] GetResourceConfigurationWarnings() => System.Array.Empty<string>();
}
