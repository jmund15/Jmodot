namespace Jmodot.Implementation.AI.BehaviorTree.Tasks;

using System.Collections.Generic;
using System.Linq;
using Core.AI.BehaviorTree;
using Core.AI.BehaviorTree.Conditions;
using GColl = Godot.Collections;
using Jmodot.Core.AI;
using Jmodot.Core.AI.BB;
using Shared;

/// <summary>
    /// The abstract base class for all nodes in a Behavior Tree.
    /// It manages status, conditions, and the basic execution lifecycle (Enter, Exit, Process).
    /// </summary>
    [GlobalClass, Tool]
    public abstract partial class BehaviorTask : Node, IBehaviorTask
    {
        private TaskStatus _status;
        private bool _exitCalled;
        /// <summary>
        /// Gets or sets the current execution status of the task.
        /// Setting this value will automatically fire the TaskStatusChanged signal.
        /// </summary>
        public TaskStatus Status
        {
            get => _status;
            protected set
            {
                if (_status == value) { return; }
                _status = value;
                EmitSignal(SignalName.TaskStatusChanged, (long)_status);
            }
        }

        /// <summary>
        /// Designer-facing condition configs. These are cloned during Init() into runtime instances.
        /// Original configs are never touched at runtime — safe to share across tasks.
        /// </summary>
        [Export] public GColl.Array<BTCondition> Conditions { get; private set; } = new();

        /// <summary>
        /// If true, this task will re-evaluate its conditions every frame while running.
        /// If a condition fails, the task will abort.
        /// Set to false for performance-critical tasks that only need a guard check on entry.
        /// </summary>
        [Export] public bool MonitorConditions { get; set; } = true;

        protected Node Agent { get; private set; } = null!;
        protected IBlackboard BB { get; private set; }  = null!;
        public string TaskName { get; private set; } = string.Empty;

        /// <summary>
        /// Runtime condition instances, cloned from [Export] Conditions during Init().
        /// All lifecycle methods operate on these, never on the original configs.
        /// </summary>
        private readonly List<BTCondition> _activeConditions = new();

        [Signal]
        public delegate void TaskStatusChangedEventHandler(TaskStatus newStatus);

        public virtual void Init(Node agent, IBlackboard bb)
        {
            this.Agent = agent;
            this.BB = bb;
            this.Status = TaskStatus.Fresh;
            this.TaskName = Name;

            // Clone condition configs into runtime instances.
            // Original [Export] configs are never modified — safe to share.
            _activeConditions.Clear();
            foreach (var config in Conditions.Where(c => c.IsValid()))
            {
                var instance = (BTCondition)config.Duplicate(true);
                instance.Init(this, agent, bb);
                _activeConditions.Add(instance);
            }
        }

    #region Test Helpers
#if TOOLS
        /// <summary>
        /// Injects a condition directly as a runtime instance, bypassing clone.
        /// For testing only. Call AFTER Init().
        /// </summary>
        internal void InjectRuntimeCondition(BTCondition condition)
        {
            condition.Init(this, Agent, BB);
            _activeConditions.Add(condition);
        }
#endif
    #endregion

        /// <summary>
        /// Template Method: Enters the task. Checks conditions first.
        /// Do not override this. Override OnEnter() for custom logic.
        /// </summary>
        public void Enter()
        {
            _exitCalled = false;
            Status = TaskStatus.Fresh;

            if (!CheckAllConditions(out _))
            {
                Status = TaskStatus.Failure;
                return;
            }

            foreach (var condition in _activeConditions)
            {
                condition.OnParentTaskEnter();
            }

            OnEnter();

            if (Status == TaskStatus.Fresh)
            {
                Status = TaskStatus.Running;
            }
        }

        /// <summary>
        /// Template Method: Unconditionally exits the task (called by parent composite).
        /// Unlike condition-based abort, Exit() always fires — there is no negotiation.
        /// Do not override this. Override OnExit() for custom logic.
        /// <para>
        /// HSM contrast: equivalent to State.Exit(). Condition-based abort with CanAbort()
        /// mirrors HSM's CanExit() / Urgent duality.
        /// </para>
        /// </summary>
        public void Exit()
        {
            if (!_exitCalled)
            {
                OnExit();
                _exitCalled = true;
            }
            foreach (var condition in _activeConditions)
            {
                condition.OnParentTaskExit();
            }
            Status = TaskStatus.Fresh;
        }

        /// <summary>
        /// Template Method: Per-frame processing.
        /// Do not override this. Override OnProcessFrame() for custom logic.
        /// </summary>
        public void ProcessFrame(float delta)
        {
            if (Status != TaskStatus.Running) { return; }

            if (MonitorConditions && !CheckAllConditions(out var failingCondition))
            {
                bool isUrgent = failingCondition!.UrgentAbort;

                if (!isUrgent && !CanAbort())
                {
                    // Abort deferred — task continues processing to wind down
                    OnProcessFrame(delta);
                    return;
                }

                JmoLogger.Info(this, $"Task aborted: {failingCondition.ResourceName}");
                if (!_exitCalled)
                {
                    OnExit();
                    _exitCalled = true;
                }
                Status = failingCondition.SucceedOnAbort ? TaskStatus.Success : TaskStatus.Failure;
                return;
            }
            OnProcessFrame(delta);
        }

        /// <summary>
        /// Template Method: Per-physics-frame processing.
        /// Do not override this. Override OnProcessPhysics() for custom logic.
        /// </summary>
        public void ProcessPhysics(float delta)
        {
            if (Status != TaskStatus.Running) { return; }
            OnProcessPhysics(delta);
        }

        /// <summary>
        /// Override this method to implement the task's entry logic.
        /// </summary>
        protected virtual void OnEnter() { }

        /// <summary>
        /// Override this method to implement the task's cleanup logic.
        /// </summary>
        protected virtual void OnExit() { }

        /// <summary>
        /// Override this method to implement the task's per-frame update logic.
        /// </summary>
        protected virtual void OnProcessFrame(float delta) { }

        /// <summary>
        /// Override this method to implement the task's per-physics-frame update logic.
        /// </summary>
        protected virtual void OnProcessPhysics(float delta) { }

        /// <summary>
        /// Called when a monitoring condition fails. Override to defer abort
        /// (e.g., wait for animation/tween to finish). Default: true (immediate abort).
        /// <para>
        /// While deferred, OnProcessFrame() continues each frame so the task can wind down.
        /// The parent composite sees Running and waits. When CanAbort() returns true on a
        /// later frame, the abort fires normally.
        /// </para>
        /// <para>
        /// Contract: MUST eventually return true, or the tree stalls.
        /// Bypassed by conditions with <see cref="BTCondition.UrgentAbort"/>=true.
        /// </para>
        /// <para>
        /// HSM equivalent: State.CanExit(). UrgentAbort mirrors StateTransition.Urgent.
        /// </para>
        /// </summary>
        protected virtual bool CanAbort() => true;

        private bool CheckAllConditions(out BTCondition? failingCondition)
        {
            failingCondition = null;
            foreach (var condition in _activeConditions)
            {
                if (condition.Check())
                {
                    continue;
                }

                failingCondition = condition;
                return false;
            }
            return true;
        }

        public override string[] _GetConfigurationWarnings()
        {
            var warnings = new List<string>();
            foreach (var child in GetChildren())
            {
                if (child is not BehaviorTask)
                {
                    warnings.Add($"Child '{child.Name}' is not a BehaviorTask. All children must derive from BehaviorTask.");
                }
            }
            return warnings.ToArray();
        }
    }
