namespace Jmodot.Implementation.AI.BehaviorTree.Tasks;

using System.Collections.Generic;
using System.Linq;
using Core.AI.BehaviorTree;
using Core.AI.BehaviorTree.Conditions;
using Godot.Collections;
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
        /// A list of conditions that must be met for this task to run or continue running.
        /// </summary>
        [Export] public Array<BTCondition> Conditions { get; private set; } = new();

        /// <summary>
        /// If true, this task will re-evaluate its conditions every frame while running.
        /// If a condition fails, the task will abort.
        /// Set to false for performance-critical tasks that only need a guard check on entry.
        /// </summary>
        [Export] public bool MonitorConditions { get; set; } = true;

        protected Node Agent { get; private set; } = null!;
        protected IBlackboard BB { get; private set; }  = null!;
        public string TaskName { get; private set; } = string.Empty;

        [Signal]
        public delegate void TaskStatusChangedEventHandler(TaskStatus newStatus);

        public virtual void Init(Node agent, IBlackboard bb)
        {
            this.Agent = agent;
            this.BB = bb;
            this.Status = TaskStatus.FRESH;
            this.TaskName = Name;

            foreach (var condition in Conditions.Where(c => c.IsValid()))
            {
                condition.Init(agent, bb);
            }
        }

        /// <summary>
        /// Template Method: Enters the task. Checks conditions first.
        /// Do not override this. Override OnEnter() for custom logic.
        /// </summary>
        public void Enter()
        {
            Status = TaskStatus.FRESH;

            if (!CheckAllConditions(out _))
            {
                Status = TaskStatus.FAILURE;
                return;
            }

            foreach (var condition in Conditions.Where(c => c.IsValid()))
            {
                condition.OnParentTaskEnter();
            }

            OnEnter();

            if (Status == TaskStatus.FRESH)
            {
                Status = TaskStatus.RUNNING;
            }
        }

        /// <summary>
        /// Template Method: Exits the task.
        /// Do not override this. Override OnExit() for custom logic.
        /// </summary>
        public void Exit()
        {
            if (Status == TaskStatus.RUNNING)
            {
                OnExit();
            }
            foreach (var condition in Conditions.Where(c => c.IsValid()))
            {
                condition.OnParentTaskExit();
            }
            Status = TaskStatus.FRESH;
        }

        /// <summary>
        /// Template Method: Per-frame processing.
        /// Do not override this. Override OnProcessFrame() for custom logic.
        /// </summary>
        public void ProcessFrame(float delta)
        {
            if (Status != TaskStatus.RUNNING) { return; }

            if (MonitorConditions && !CheckAllConditions(out var failingCondition))
            {
                JmoLogger.Info(this, $"Task aborted due to failed condition monitoring: {failingCondition!.ResourceName}");
                Status = failingCondition.SucceedOnAbort ? TaskStatus.SUCCESS : TaskStatus.FAILURE;
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
            if (Status != TaskStatus.RUNNING) { return; }
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

        private bool CheckAllConditions(out BTCondition? failingCondition)
        {
            failingCondition = null;
            foreach (var condition in Conditions.Where(c => c.IsValid()))
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
