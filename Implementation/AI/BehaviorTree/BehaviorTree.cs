namespace Jmodot.Implementation.AI.BehaviorTree;

using System.Collections.Generic;
using System.Linq;
using Composites;
using Core.AI.BB;
using Tasks;

[GlobalClass]
[Tool]
public partial class BehaviorTree : Node
{
    #region TREE_VARIABLES

    public string TreeName { get; protected set; } = string.Empty;
    public bool Initialized { get; protected set; }

    [Export] private Node _exportedBB;
    [Export] public Node AgentNode { get; set; }
    public IBlackboard BB { get; set; }

    private bool _enabled;

    [Export]
    public bool Enabled
    {
        get => this._enabled;
        set
        {
            if (this._enabled == value) return;
            this._enabled = value;
            if (this._enabled)
                this.EmitSignal(SignalName.TreeEnabled);
            else
                this.EmitSignal(SignalName.TreeDisabled);
        }
    }

    [Export] // Runs without needing init'd from another node
    public bool SelfSuffecient { get; protected set; }

    public BehaviorTask RootTask { get; set; }
    public BehaviorAction RunningLeaf { get; protected set; }
    public float ProcTimeMetric { get; private set; }

    [Signal]
    public delegate void TreeInitializedEventHandler();

    [Signal]
    public delegate void TreeEnabledEventHandler();

    [Signal]
    public delegate void TreeDisabledEventHandler();

    [Signal]
    public delegate void TreeFinishedLoopEventHandler(BTaskStatus treeStatus);

    [Signal]
    public delegate void TreeResetEventHandler();

    #endregion

    #region TREE_UPDATES

    public override void _Ready()
    {
        base._Ready();
        //TreeInitialized += () => { Initialized = true; GD.Print("TREE INITIALIZD"); };

        if (this._enabled && this.SelfSuffecient)
        {
            if (Engine.IsEditorHint())
            {
                this.TreeName = "Editor's Behavior Tree";
            }
            else if (!this.AgentNode.IsValid())
            {
                GD.PrintErr("BehaviorTree ERROR || AgentNode is not valid!");
                return;
            }
            else
            {
                this.TreeName = this.AgentNode.Name + "'s Behavior Tree";
            }

            if (this._exportedBB is not IBlackboard bb)
            {
                GD.PrintErr("BehaviorTree ERROR || Exported Blackboard doesn't implement \"IBlackboard\"!");
                return;
            }
            //CallDeferred(MethodName.Init, AgentNode, bb);
            //CallDeferred(MethodName.Enter);

            this.CallDeferred(MethodName.InitTreeAndEnter);
        }
    }

    private void InitTreeAndEnter()
    {
        this.Init(this.AgentNode, this._exportedBB as IBlackboard);
        this.Enter();
        GD.Print("entered self enabled BT!");
    }

    public override void _Process(double delta)
    {
        if (Engine.IsEditorHint()) return;
        base._Process(delta);
        if (this.Enabled && this.Initialized) this.ProcessFrame((float)delta);
    }

    public override void _PhysicsProcess(double delta)
    {
        if (Engine.IsEditorHint()) return;
        base._PhysicsProcess(delta);
        if (this.Enabled && this.Initialized) this.ProcessPhysics((float)delta);
    }

    public virtual void Init(Node agent, IBlackboard bb)
    {
        this.AgentNode = agent;
        this.BB = bb;
        if (this.TreeName == string.Empty) this.TreeName = this.AgentNode.Name + "'s Behavior Tree";
        var rt = this.GetFirstChildOfType<BehaviorTask>();
        if (!rt.IsValid())
        {
            this.Enabled = false;
            GD.PrintErr("BEHAVIOR TREE HAS NO ROOT TASK");
            return;
        }

        this.RootTask = rt;
        this.RootTask.Init(this.AgentNode, this.BB);
        this.EmitSignal(SignalName.TreeInitialized);
        this.Initialized = true;
        //GD.Print("root task of tree: ", RootTask.TaskName);
    }

    public virtual void Enter()
    {
        this.Enabled = true;
        this.RootTask.Enter();
        this.RootTask.TaskStatusChanged += this.OnRootTaskStatusChanged;

        //GetRunningLeaf();
    }

    public virtual void Exit()
    {
        this.Enabled = false;
        this.RootTask.Exit();
        this.RootTask.TaskStatusChanged -= this.OnRootTaskStatusChanged;
        //GD.Print($"BTree {Name} Exited.");
    }

    public virtual void ProcessFrame(float delta)
    {
        this.RootTask.ProcessFrame(delta);
    }

    public virtual void ProcessPhysics(float delta)
    {
        this.RootTask.ProcessPhysics(delta);
    }

    #endregion

    #region TREE_HELPER

    private void OnRootTaskStatusChanged(BTaskStatus newStatus)
    {
        GD.Print($"Tree root node {this.RootTask.Name} status changed to {newStatus}");
        switch (newStatus)
        {
            case BTaskStatus.RUNNING or BTaskStatus.FRESH:
                break;
            case BTaskStatus.SUCCESS:
                this.EmitSignal(SignalName.TreeFinishedLoop, Variant.From(BTaskStatus.SUCCESS));
                GD.Print("EMITTED TREE FINISHED WITH SUCCESS");
                if (this.Enabled)
                {
                    this.RootTask.Exit();
                    this.RootTask.Enter();
                    this.EmitSignal(SignalName.TreeReset);
                }

                break;
            case BTaskStatus.FAILURE:
                this.EmitSignal(SignalName.TreeFinishedLoop, Variant.From(BTaskStatus.FAILURE));
                GD.Print("EMITTED TREE FINISHED WITH FAILURE");
                if (this.Enabled)
                {
                    this.RootTask.Exit();
                    this.RootTask.Enter();
                    this.EmitSignal(SignalName.TreeReset);
                }

                break;
        }
    }

    protected virtual void
        GetRunningLeaf() // TODO: DOESN'T WORK (signal will emit before running leaf has actually switched (probably))
    {
        var currLeaf = this.RootTask;
        while (currLeaf is not BehaviorAction) //&& currLeaf.Status != BTaskStatus.RUNNING) //UNNECESARY
        {
            if (currLeaf is not CompositeTask compT)
            {
                GD.PrintErr("WEIRD BT ERROR HELP!");
                return;
            }

            currLeaf = compT.RunningChild;
        }

        this.RunningLeaf = currLeaf as BehaviorAction;
        this.RunningLeaf.TaskStatusChanged += status => this.GetRunningLeaf();
    }

    public override string[] _GetConfigurationWarnings()
    {
        var warnings = new List<string>();

        if (this.GetChildren().Count > 1)
            warnings.Add("BehaviorTree nodes should only have one child (the root BehaviorTask)");
        if (this.GetChildren().Any(x => x is not BehaviorTask))
            warnings.Add("Root BehaviorTree should inherit from BehaviorTask class.");
        if (this.BB is not null && this.BB is not IBlackboard bb)
            warnings.Add("The exported Blackboard must implement \"IBlackboard\"!");
        return warnings.ToArray();
    }

    #endregion
}
