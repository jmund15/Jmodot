namespace Jmodot.Implementation.AI.BehaviorTree;

using System.Collections.Generic;
using System.Linq;
using Composites;
using Core.AI;
using Core.AI.BB;
using Shared;
using Shared.GodotExceptions;
using Tasks;

/// <summary>
/// The root controller for a Behavior Tree. It manages the lifecycle (initialization, entering, exiting)
/// and the continuous "ticking" of its root task. It serves as the entry point for executing
/// a tree of behaviors, either driven by an external system (like a BTState) or running independently.
/// </summary>
[GlobalClass, Tool]
public partial class BehaviorTree : Node
{
    #region EXPORTS & PROPERTIES

    /// <summary>
    /// The agent (Node) that this behavior tree will control.
    /// </summary>
    [Export] public Node AgentNode { get; private set; }

    /// <summary>
    /// The Blackboard resource used for data sharing among tasks. Must implement IBlackboard.
    /// </summary>
    [Export] private Node _blackboardNode;
    public IBlackboard Blackboard { get; private set; }

    /// <summary>
    /// If true, this tree will attempt to initialize and run itself in _Ready.
    /// Useful for testing or for agents whose entire logic is contained within a single tree.
    /// Requires AgentNode and Blackboard to be set in the editor.
    /// </summary>
    [Export] public bool SelfSufficient { get; private set; }

    public enum DebugViewPosition
    {
        TopLeft,
        TopRight,
        BottomLeft,
        BottomRight
    }
    [ExportGroup("Debugging")]
    /// <summary>
    /// If true, a debug overlay will be instantiated to visualize the tree's activity at runtime.
    /// </summary>
    [Export] private bool _enableDebugView = false;

    [Export] private DebugViewPosition _debugViewPosition = DebugViewPosition.TopLeft;

    public bool IsInitialized { get; private set; }
    public BehaviorTask RootTask { get; private set; }

    private bool _enabled;
    public bool Enabled
    {
        get => _enabled;
        private set
        {
            if (_enabled == value) return;
            _enabled = value;
            EmitSignal(_enabled ? SignalName.TreeEnabled : SignalName.TreeDisabled);
        }
    }

    #endregion

    #region SIGNALS

    [Signal] public delegate void TreeInitializedEventHandler();
    [Signal] public delegate void TreeEnabledEventHandler();
    [Signal] public delegate void TreeDisabledEventHandler();
    [Signal] public delegate void TreeFinishedLoopEventHandler(TaskStatus treeStatus);
    [Signal] public delegate void TreeResetEventHandler();

    #endregion

    #region LIFECYCLE & PROCESSING

    public override void _Ready()
    {
        if (Engine.IsEditorHint() || !SelfSufficient) return;

        if (!AgentNode.IsValid())
        {
            JmoLogger.Error(this, "SelfSufficient tree cannot start: AgentNode is not set.");
            return;
        }
        if (_blackboardNode is not IBlackboard bb)
        {
            JmoLogger.Error(this, "SelfSufficient tree cannot start: Blackboard is not set or does not implement IBlackboard.");
            return;
        }

        Init(AgentNode, bb);
        Enter();
    }

    public override void _Process(double delta)
    {
        if (Engine.IsEditorHint() || !Enabled || !IsInitialized) return;
        RootTask?.ProcessFrame((float)delta);
    }

    public override void _PhysicsProcess(double delta)
    {
        if (Engine.IsEditorHint() || !Enabled || !IsInitialized) return;
        RootTask?.ProcessPhysics((float)delta);
    }

    /// <summary>
    /// Initializes the Behavior Tree and all its child tasks.
    /// This must be called before Enter().
    /// </summary>
    /// <param name="agent">The Node that the tree will be controlling.</param>
    /// <param name="bb">The IBlackboard instance for data communication.</param>
    public virtual void Init(Node agent, IBlackboard bb)
    {
        if (IsInitialized) return;

        AgentNode = agent;
        Blackboard = bb;

        if (!this.TryGetFirstChildOfType<BehaviorTask>(out var rootTask, false))
        {
            throw new NodeConfigurationException($"BehaviorTree '{Name}' requires a child node that inherits from BehaviorTask to serve as its root.", this);
        }

        RootTask = rootTask;
        RootTask.Init(AgentNode, Blackboard);

        if (_enableDebugView && !Engine.IsEditorHint())
        {
            var canvas = new CanvasLayer { Name = "BTDebugCanvas" };
            AddChild(canvas);
            var debugComponent = new DebugBTComponent();
            AddChild(debugComponent); // Add the UI as a child of the tree itself.
            debugComponent.Init(this); // Initialize it with a reference to this tree.
            debugComponent.SetDisplayPosition(_debugViewPosition); // New method call
        }

        IsInitialized = true;
        EmitSignal(SignalName.TreeInitialized);
    }

    /// <summary>
    /// Activates the Behavior Tree, causing it to start processing.
    /// </summary>
    public virtual void Enter()
    {
        if (!IsInitialized)
        {
            JmoLogger.Error(this, "Attempted to Enter() BehaviorTree before it was initialized. Call Init() first.");
            return;
        }

        Enabled = true;
        RootTask.TaskStatusChanged += OnRootTaskStatusChanged;
        RootTask.Enter();
    }

    /// <summary>
    /// Deactivates the Behavior Tree, stopping all processing.
    /// </summary>
    public virtual void Exit()
    {
        if (!IsInitialized) return;

        Enabled = false;
        RootTask.TaskStatusChanged -= OnRootTaskStatusChanged;
        RootTask.Exit();
    }

    #endregion

    #region EVENT_HANDLERS & HELPERS

    private void OnRootTaskStatusChanged(TaskStatus newStatus)
    {
        // We only care about terminal states (SUCCESS or FAILURE) at the root level.
        if (newStatus is TaskStatus.RUNNING or TaskStatus.FRESH)
        {
            return;
        }

        JmoLogger.Info(this, $"Tree root task '{RootTask.Name}' finished with status {newStatus}.");
        EmitSignal(SignalName.TreeFinishedLoop, (long)newStatus);

        // If the tree is still enabled after the loop finished (i.e., not being shut down by a BTState),
        // automatically reset and restart it for continuous execution.
        if (Enabled)
        {
            JmoLogger.Info(this, "Resetting and restarting tree...");
            EmitSignal(SignalName.TreeReset);
            RootTask.Exit();  // Ensure a clean exit before re-entering.
            RootTask.Enter();
        }
    }

    public override string[] _GetConfigurationWarnings()
    {
        var warnings = new List<string>();

        var children = GetChildren();
        if (children.Count(c => c is BehaviorTask) == 0)
        {
            warnings.Add("BehaviorTree must have one child that inherits from BehaviorTask to act as the root.");
        }
        if (children.Count(c => c is BehaviorTask) > 1)
        {
            warnings.Add("BehaviorTree should only have one BehaviorTask child (the root task).");
        }

        if (SelfSufficient)
        {
            if (AgentNode == null) warnings.Add("SelfSufficient is true, but AgentNode is not assigned.");
            if (_blackboardNode == null) warnings.Add("SelfSufficient is true, but Blackboard is not assigned.");
            else if (_blackboardNode is not IBlackboard) warnings.Add("The assigned Blackboard node must implement the IBlackboard interface.");
        }

        return warnings.ToArray();
    }

    #endregion
}
