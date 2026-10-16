namespace Jmodot.Implementation.AI.BehaviorTree;

using System.Collections.Generic;
using System.Linq;
using Composites;
using Core.AI;
using Core.AI.BB;
using Debug;
using HSM;
using Shared;
using Shared.GodotExceptions;
using Tasks;

/// <summary>
/// The root controller for a Behavior Tree. It manages the lifecycle (initialization, entering, exiting)
/// and the continuous "ticking" of its root task. It serves as the entry point for executing
/// a tree of behaviors, either driven by an external system (like a BTState) or running independently.
/// </summary>
[GlobalClass, Tool]
public partial class BehaviorTree : Node, IDebugPanelProvider
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

    /// <summary>
    /// Governs the tree's behavior when its root task reaches a terminal status (Success or Failure).
    /// </summary>
    public enum RestartPolicy
    {
        /// <summary>
        /// Default. Re-Enter the root immediately on terminal status. Fires <see cref="TreeReset"/>.
        /// Suited to ambient/looping behavior trees (idle wander, perception scans).
        /// </summary>
        AlwaysRestart,

        /// <summary>
        /// Hold the root at its terminal status. The tree stays Enabled, but no further processing
        /// happens until an external system calls <see cref="Exit"/> + <see cref="Enter"/> to re-arm.
        /// Suited to BTs whose lifecycle is owned by an HSM state — completion is a meaningful signal,
        /// not a trigger to loop. Removes the indefinite-action workaround codified in
        /// Memory:Critter_AI_Architecture.
        /// </summary>
        HoldTerminal,

        /// <summary>
        /// Disable the tree on terminal status (fires <see cref="TreeDisabled"/>) after exiting the
        /// root task to allow OnExit cleanup. The tree is dormant until <see cref="Enter"/> is called
        /// manually. Suited to one-shot composable sequences (a craft combo, a one-time scripted move).
        /// <para>
        /// Callers that need the terminal status MUST read it from the <see cref="TreeFinishedLoop"/>
        /// signal payload — <c>RootTask.Status</c> is reset to <see cref="TaskStatus.Fresh"/> by the
        /// Exit() call so the root task's resources (subscriptions, BB keys, timers) are cleaned up.
        /// </para>
        /// </summary>
        OneShot,
    }

    /// <summary>
    /// Determines what happens when the root task reaches a terminal (Success/Failure) status.
    /// Default <see cref="RestartPolicy.AlwaysRestart"/> preserves pre-existing behavior.
    /// </summary>
    [Export] public RestartPolicy Policy { get; private set; } = RestartPolicy.AlwaysRestart;

    /// <summary>
    /// If true, a debug overlay will be instantiated to visualize the tree's activity at runtime.
    /// </summary>
    [ExportGroup("Debugging")]
    [Export] private bool _enableDebugView = false;

    [Export] private DebugAIPanel.DebugViewPosition _debugViewPosition = DebugAIPanel.DebugViewPosition.TopLeft;

    private const int MaxDeliveriesPerFrame = 8;

    private readonly Queue<TaskStatus> _pendingTerminals = new();
    private bool _delivering;
    private bool _backoff;
    private int _deliveriesThisFrame;
    private int _deferredInEpisode;
    private double _simSeconds;
    private double _nextFinishedLogAt;
    private double _nextRestartLogAt;

    public bool IsInitialized { get; private set; }
    public BehaviorTask RootTask { get; private set; } = null!; // Set in Init(), exception if missing

    private bool _enabled;
    public bool Enabled
    {
        get => _enabled;
        private set
        {
            if (_enabled == value) { return; }
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

    #region IDebugPanelProvider

    public string DebugTabName => GetParent() is BTState btState
        ? $"{btState.Name.ToString().Replace("State", "")} BT"
        : Name;

    public int DebugTabOrder => 10 + GetIndex();

    public bool HasDebugData => RootTask != null;

    public Control CreateDebugContent()
    {
        if (RootTask == null) { return new Label { Text = $"{Name}: Not initialized" }; }

        var debugBT = new DebugBTComponent { EmbeddedMode = true };
        debugBT.Init(RootTask, Name, AgentNode as Node3D);
        debugBT.BuildTreeView();

        TreeEnabled += debugBT.OnTreeEnabled;
        TreeDisabled += debugBT.OnTreeDisabled;
        TreeReset += debugBT.OnTreeReset;

        debugBT.TreeExiting += () =>
        {
            TreeEnabled -= debugBT.OnTreeEnabled;
            TreeDisabled -= debugBT.OnTreeDisabled;
            TreeReset -= debugBT.OnTreeReset;
        };

        // If BT is already enabled, show immediately (signal was emitted before subscription)
        if (Enabled)
        {
            debugBT.OnTreeEnabled();
        }

        return debugBT;
    }

    public void UpdateDebugContent(double delta)
    {
        // Timer updates handled by DebugBTComponent's own _Process
    }

    public void OnDebugContentHidden()
    {
        // No expensive work to pause
    }

    #endregion

    #region LIFECYCLE & PROCESSING

    public override void _Ready()
    {
        if (Engine.IsEditorHint() || !SelfSufficient) { return; }

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
        if (Engine.IsEditorHint() || !Enabled || !IsInitialized) { return; }
        RootTask?.ProcessFrame((float)delta);
    }

    public override void _PhysicsProcess(double delta)
    {
        _deliveriesThisFrame = 0;
        _simSeconds += delta;
        if (Engine.IsEditorHint() || !Enabled || !IsInitialized) { return; }

        DrainPendingAtFrameHead();
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
        if (IsInitialized) { return; }

        AgentNode = agent;
        Blackboard = bb;

        if (!this.TryGetFirstChildOfType<BehaviorTask>(out var rootTask, false) || rootTask == null)
        {
            throw new NodeConfigurationException($"BehaviorTree '{Name}' requires a child node that inherits from BehaviorTask to serve as its root.", this);
        }

        RootTask = rootTask;
        RootTask.Init(AgentNode, Blackboard);

        // Walk up to find the entity root and check for dashboard presence
        bool dashboardPresent = false;
        var entityRoot = GetParent()?.GetParent(); // BT → BTState → CompoundState → Entity (or BT → Entity)
        if (GetParent() is BTState)
        {
            entityRoot = GetParent()?.GetParent()?.GetParent(); // BT → BTState → CompoundState → Entity
        }
        if (entityRoot != null)
        {
            dashboardPresent = entityRoot.TryGetFirstChildOfType<AIDebugDashboard>(out _, true);
        }
        if (_enableDebugView && !Engine.IsEditorHint() && !dashboardPresent)
        {
            var debugComponent = new DebugBTComponent();
            AddChild(debugComponent);
            debugComponent.Init(RootTask, Name, AgentNode as Node3D);
            debugComponent.SetDisplayPosition(_debugViewPosition);
            debugComponent.BuildTreeView();

            TreeEnabled += debugComponent.OnTreeEnabled;
            TreeDisabled += debugComponent.OnTreeDisabled;
            TreeReset += debugComponent.OnTreeReset;
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

        ResetDelivery();
        Enabled = true;
        RootTask.TaskStatusChanged += OnRootTaskStatusChanged;
        RootTask.Enter();
    }

    /// <summary>
    /// Deactivates the Behavior Tree, stopping all processing.
    /// </summary>
    public virtual void Exit()
    {
        if (!IsInitialized) { return; }

        ResetDelivery();
        Enabled = false;
        RootTask.TaskStatusChanged -= OnRootTaskStatusChanged;
        RootTask.Exit();
    }

    #endregion

    #region EVENT_HANDLERS & HELPERS

    private void OnRootTaskStatusChanged(TaskStatus newStatus)
    {
        // We only care about terminal states (SUCCESS or FAILURE) at the root level.
        if (newStatus is TaskStatus.Running or TaskStatus.Fresh)
        {
            return;
        }

        if (_delivering || _deliveriesThisFrame >= DeliveryLimit)
        {
            _pendingTerminals.Enqueue(newStatus);
            return;
        }

        DeliverAndDrain(newStatus);
    }

    /// <summary>
    /// A terminal that arrives while another is being delivered (a root that fails inside its own
    /// <c>Enter</c>, or a hosting state that answers <see cref="TreeFinishedLoop"/> with Exit + Enter) is queued
    /// and delivered after the outer call returns, so same-frame stack depth stays flat.
    /// </summary>
    private void DeliverAndDrain(TaskStatus first)
    {
        _delivering = true;
        try
        {
            var status = first;
            while (true)
            {
                DeliverTerminal(status);
                if (!TryDequeueDeliverable(out status)) { break; }
            }
        }
        finally
        {
            _delivering = false;
        }

        NoteDeferred();
    }

    private int DeliveryLimit => _backoff ? 1 : MaxDeliveriesPerFrame;

    private bool TryDequeueDeliverable(out TaskStatus status)
    {
        status = default;
        while (_pendingTerminals.Count > 0)
        {
            if (_deliveriesThisFrame >= DeliveryLimit) { return false; }

            var queued = _pendingTerminals.Dequeue();
            if (Enabled && RootTask.Status == queued)
            {
                status = queued;
                return true;
            }
        }

        return false;
    }

    private void NoteDeferred()
    {
        if (_pendingTerminals.Count == 0) { return; }

        if (!_backoff)
        {
            _backoff = true;
            JmoLogger.Warning(this, $"[BT] terminal delivery capped at {MaxDeliveriesPerFrame} per frame for '{Name}'; deferring the rest, one per frame.");
        }

        _deferredInEpisode += _pendingTerminals.Count;
    }

    private void DrainPendingAtFrameHead()
    {
        if (_pendingTerminals.Count > 0 && !_delivering)
        {
            if (TryDequeueDeliverable(out var status)) { DeliverAndDrain(status); }
            else { NoteDeferred(); }
        }

        if (_backoff && _pendingTerminals.Count == 0 && RootTask.Status == TaskStatus.Running) { EndBackoff(); }
    }

    private void EndBackoff()
    {
        if (!_backoff) { return; }

        _backoff = false;
        JmoLogger.Warning(this, $"[BT] terminal delivery resumed, {_deferredInEpisode} deferred for '{Name}'.");
        _deferredInEpisode = 0;
    }

    /// <summary>
    /// A restart that begins outside any delivery starts a new cascade: the queue is dropped (its entries
    /// belong to the previous root run), and unless a delivery is on the stack the frame count and any backoff
    /// episode end with it. A reentrant Exit + Enter keeps both, so a hosting state's self-transition stays capped.
    /// </summary>
    private void ResetDelivery()
    {
        _pendingTerminals.Clear();
        if (_delivering) { return; }

        _deliveriesThisFrame = 0;
        EndBackoff();
    }

    private bool MayLogDelivery(ref double nextAllowedAt)
    {
        var throttled = _backoff || _deliveriesThisFrame > 1;
        if (throttled && _simSeconds < nextAllowedAt) { return false; }

        nextAllowedAt = _simSeconds + 1.0;
        return true;
    }

    private void DeliverTerminal(TaskStatus newStatus)
    {
        _deliveriesThisFrame++;
        if (MayLogDelivery(ref _nextFinishedLogAt))
        {
            JmoLogger.Debug(this, $"[BT] Tree root task '{RootTask.Name}' finished with status {newStatus}.");
        }

        EmitSignal(SignalName.TreeFinishedLoop, (long)newStatus);

        // Tree was already disabled (e.g., external Exit() raced with terminal status) — nothing to do.
        if (!Enabled) { return; }

        switch (Policy)
        {
            case RestartPolicy.AlwaysRestart:
                if (MayLogDelivery(ref _nextRestartLogAt))
                {
                    JmoLogger.Info(this, "[BT] Resetting and restarting tree...");
                }

                EmitSignal(SignalName.TreeReset);
                RootTask.Exit();  // Ensure a clean exit before re-entering.
                RootTask.Enter();
                break;

            case RestartPolicy.HoldTerminal:
                // Tree stays Enabled but root holds its terminal status. External system
                // (typically an HSM transition or owning state) calls Exit+Enter to re-arm.
                // Subscription stays armed — nothing else can change the root's status spontaneously.
                break;

            case RestartPolicy.OneShot:
                // Unsubscribe BEFORE Exit so the implicit Status = Fresh in BehaviorTask.Exit
                // does not re-enter this handler. Then Exit the root so OnExit cleanup fires
                // (signal unsubscribes, BB key clears, timer cancels — anything the root or its
                // descendants registered in their OnEnter cascade). Finally disable the tree
                // (fires TreeDisabled via the Enabled setter). Terminal status was already
                // emitted via TreeFinishedLoop above — callers read it from there.
                RootTask.TaskStatusChanged -= OnRootTaskStatusChanged;
                RootTask.Exit();
                Enabled = false;
                break;
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
            if (AgentNode == null) { warnings.Add("SelfSufficient is true, but AgentNode is not assigned."); }
            if (_blackboardNode == null) { warnings.Add("SelfSufficient is true, but Blackboard is not assigned."); }
            else if (_blackboardNode is not IBlackboard) { warnings.Add("The assigned Blackboard node must implement the IBlackboard interface."); }
        }

        return warnings.ToArray();
    }

    #endregion

    #region Test Helpers
#if TOOLS
    /// <summary>
    /// Sets <see cref="Policy"/> directly for unit tests, bypassing the Inspector-only
    /// private setter. Logic-Domain tests cover each policy's terminal behavior; this
    /// helper avoids reflection in those tests.
    /// </summary>
    internal void SetPolicyForTesting(RestartPolicy policy) => Policy = policy;
#endif
    #endregion
}
