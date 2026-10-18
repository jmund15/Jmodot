namespace Jmodot.Implementation.AI.BehaviorTree.Tasks;

using System.Collections.Generic;
using System.Linq;
using Core.AI.BB;
using Core.AI.BehaviorTree;
using Core.AI.BehaviorTree.Conditions;
using Core.AI.Navigation.Considerations;
using Core.AI.Perception;
using Core.AI;
using Core.Shared;
using Jmodot.AI.Navigation;
using Jmodot.Examples.AI.BehaviorTree.Conditions;
using Jmodot.Implementation.AI.BB;
using Jmodot.Implementation.AI.Navigation.Goals;
using Jmodot.Implementation.AI.Navigation.Targets;
using Jmodot.Implementation.AI.Perception;
using Jmodot.Implementation.AI.BehaviorTree.Composites;
using Shared;
using Shared.GodotExceptions;

/// <summary>
/// One movement step: a goal owns where the agent goes and when the step is done; the inherited
/// style terms and overrides only bend how it gets there. The goal registers as one steering term
/// (weighted and named after the goal) and never as a second consideration hierarchy.
/// </summary>
[GlobalClass, Tool]
public partial class MoveAction : SteeringBehaviorAction
{
    /// <summary>
    /// Where this step goes and when it ends. Required: an empty slot warns in the Inspector and
    /// throws at init. Empty targets follow the primary target, so the common case needs no key.
    /// </summary>
    [Export] public MovementGoal3D? Goal { get; private set; }

    private GoalSteeringTerm? _adapter;
    private AIConsiderationRuntime? _runtime;
    private AIPerceptionManager3D? _memory;
    private bool _goalEntered;
    private bool _goalRegistered;
    private bool _claimGranted;

    public override void Init(Node agent, IBlackboard bb)
    {
        base.Init(agent, bb);

        if (agent is not Node3D)
        {
            throw new NodeConfigurationException(
                $"{nameof(MoveAction)} '{Name}' needs a Node3D agent to read a movement position from.", this);
        }

        if (Goal == null)
        {
            throw new NodeConfigurationException("Goal: empty slot — nothing is assigned here.", this);
        }

        var faults = Goal.GetResourceConfigurationWarnings();
        if (faults.Length > 0)
        {
            throw new NodeConfigurationException(FirstGoalFault(faults), this);
        }

        var agentFaults = Goal.GetAgentWarnings(agent);
        if (agentFaults.Length > 0)
        {
            throw new NodeConfigurationException(FirstGoalFault(agentFaults), this);
        }
    }

    private string FirstGoalFault(string[] faults)
    {
        var rendered = ConfigWarnings.ResourceSlotWarnings("Goal", Goal, faults);
        return rendered.Length > 0 ? rendered[0] : "Goal: unusable configuration.";
    }

    public override string[] _GetConfigurationWarnings()
    {
        var warnings = new List<string>(base._GetConfigurationWarnings());
        warnings.AddRange(ConfigWarnings.ResourceSlotWarnings("Goal", Goal, required: true));
        if (Goal == null) { return warnings.ToArray(); }

        // The editor has no Init'ed agent, so the action itself stands in for the entity walk.
        var agentForWarnings = (Node?)Agent ?? this;
        warnings.AddRange(ConfigWarnings.ResourceSlotWarnings("Goal", Goal, Goal.GetAgentWarnings(agentForWarnings)));

        if (Goal.Steers && Goal.SteeringWeight == 0f)
        {
            warnings.Add($"Goal '{GoalName()}' has Weight 0, so it is registered but exerts no pull. " +
                         "Raise its weight or remove the goal.");
        }

        if (Goal.ControlMode == SteeringControlMode.NavigationOnly)
        {
            foreach (var consideration in Considerations)
            {
                if (consideration is IAmbientSteeringConsideration) { continue; }
                var name = consideration == null ? "an empty style slot" : ResourceLabel(consideration);
                warnings.Add($"Style term '{name}' will not score while this goal claims NavigationOnly. " +
                             "Keep only ambient terms on a navigation-only step.");
            }
        }

        if (NeverEndingWarning() is { } neverEnding) { warnings.Add(neverEnding); }

        return warnings.ToArray();
    }

    private string GoalName()
        => Goal == null ? "?" : string.IsNullOrEmpty(Goal.ResourceName) ? Goal.GetType().Name : Goal.ResourceName;

    private static string ResourceLabel(Resource resource)
        => string.IsNullOrEmpty(resource.ResourceName) ? resource.GetType().Name : resource.ResourceName;

    protected override void OnEnter()
    {
        _goalEntered = false;
        _goalRegistered = false;
        _claimGranted = false;

        if (!TryGetSteering(out var steering))
        {
            JmoLogger.Error(this, $"{nameof(MoveAction)} '{Name}': no AISteeringProcessor3D on the agent.");
            Status = TaskStatus.Failure;
            return;
        }

        foreach (var consideration in Considerations)
        {
            if (consideration == null)
            {
                JmoLogger.Error(this, $"{nameof(MoveAction)} '{Name}': a style slot is empty; assign or remove it.");
                Status = TaskStatus.Failure;
                return;
            }
            if (steering.IsConsiderationRegistered(consideration))
            {
                JmoLogger.Error(this,
                    $"{nameof(MoveAction)} '{Name}': style term '{ResourceLabel(consideration)}' is already " +
                    "registered by another owner; this step will not take it over.");
                Status = TaskStatus.Failure;
                return;
            }
        }

        base.OnEnter();
        if (Status == TaskStatus.Failure) { return; }
        if (Goal == null) { Status = TaskStatus.Failure; return; }

        _adapter ??= new GoalSteeringTerm();
        _adapter.Bind(Goal);
        _runtime = steering.GetOrCreateRuntime(_adapter);
        _memory = BB.TryGet<AIPerceptionManager3D>(BBDataSig.PerceptionComp, out var memory) ? memory : null;

        if (Goal.ControlMode != SteeringControlMode.Full)
        {
            if (!steering.TryClaimControl(Name, Goal.ControlMode))
            {
                JmoLogger.Error(this,
                    $"{nameof(MoveAction)} '{Name}': steering control is already held elsewhere; this step fails " +
                    "without releasing the other claim.");
                Status = TaskStatus.Failure;
                return;
            }
            _claimGranted = true;
        }

        if (Goal.Steers)
        {
            steering.RegisterConsideration(_adapter);
            _goalRegistered = true;
        }

        Goal.OnEnter(Context(0f), _runtime);
        _goalEntered = true;
    }

    protected override void OnProcessSteeringPhysics(float delta)
    {
        if (!_goalEntered || Goal == null)
        {
            Status = TaskStatus.Failure;
            return;
        }

        var result = Goal.Advance(Context(delta), _runtime);
        Status = result switch
        {
            GoalStatus.Succeeded => TaskStatus.Success,
            GoalStatus.Failed => TaskStatus.Failure,
            _ => TaskStatus.Running,
        };
    }

    protected override void OnExit()
    {
        var goalEntered = _goalEntered;
        var registered = _goalRegistered;
        var claimGranted = _claimGranted;
        _goalEntered = false;
        _goalRegistered = false;
        _claimGranted = false;

        if (goalEntered && Goal != null)
        {
            Goal.OnExit(Context(0f), _runtime);
        }

        base.OnExit();

        if (!TryGetSteering(out var steering)) { return; }
        if (registered) { steering.UnregisterConsideration(_adapter!); }
        if (claimGranted) { steering.ReleaseControl(Name); }
    }

    private MovementGoalContext Context(float delta)
        => new(((Node3D)Agent).GlobalPosition, BB, _memory, delta);

    /// <summary>
    /// A never-ending goal starves later work when it sits in a Sequence branch with a later
    /// sibling, a plain Selector with any sibling, or anywhere under an EngageCycle. A time limit
    /// on the step or an ancestor (or any SucceedOnAbort condition) ends it, so it does not warn.
    /// </summary>
    private string? NeverEndingWarning()
    {
        if (Goal == null || Goal.EndsItself) { return null; }

        Node branch = this;
        for (var node = (Node?)this; node != null; node = node.GetParent())
        {
            if (node is BehaviorTask task && task.Conditions.Any(IsEndingLimit)) { return null; }

            if (node is EngageCycle)
            {
                return NeverEndingMessage("an EngageCycle, which would cycle on it forever");
            }

            if (node is Sequence && HasLaterSibling(node, branch))
            {
                return NeverEndingMessage("a Sequence branch with a later sibling, which this step would starve");
            }

            if (node is Selector && HasAnySibling(node, branch))
            {
                return NeverEndingMessage("a Selector branch with siblings, which this step would starve");
            }

            branch = node;
        }
        return null;
    }

    private string NeverEndingMessage(string where)
        => $"Goal '{GoalName()}' never ends itself and sits in {where}. Add a time limit to this step " +
           "or an ancestor, or use a goal that ends.";

    private static bool IsEndingLimit(BTCondition condition)
        => condition is TimeLimit || condition is RandomTimeLimit || condition.SucceedOnAbort;

    private static bool HasLaterSibling(Node composite, Node branch)
    {
        var siblings = composite.GetChildren().OfType<BehaviorTask>().ToList();
        var index = siblings.IndexOf(branch as BehaviorTask);
        return index >= 0 && index < siblings.Count - 1;
    }

    private static bool HasAnySibling(Node composite, Node branch)
        => composite.GetChildren().OfType<BehaviorTask>().Count() > 1;

    #region Test Helpers
#if TOOLS
    internal void SetGoalForTest(MovementGoal3D? goal) => Goal = goal;
    internal AIConsiderationRuntime? RuntimeForTest => _runtime;
    internal BaseAIConsideration3D? TermForTest => _adapter;
#endif
    #endregion
}
