// --- UtilitySelector.cs (Jmodot Compatible) ---
namespace Jmodot.Implementation.AI.UtilityAI;

using System.Collections.Generic;
using System.Linq;
using Godot;
using Core.AI;
using Core.AI.BB;
using BehaviorTree.Composites;
using BehaviorTree.Tasks;
using Shared;

public enum TieBreaker
{
    HighestPriority, // Child with higher priority wins
    FirstInList,     // The one that appears first in the scene tree wins
    Random           // Choose randomly among the highest-scoring children
}

/// <summary>
/// A Utility AI composite that selects the highest-scoring action from its children.
/// Children must implement IUtilityTask. Periodically reassesses to potentially switch actions.
/// </summary>
[GlobalClass, Tool]
public partial class UtilitySelector : CompositeTask
{
    #region TASK_VARIABLES
    [Export]
    private float _reassessmentInterval = 0.5f; // How often to re-evaluate the best action. 0 = every frame.
    [Export]
    private TieBreaker _tieBreaker = TieBreaker.HighestPriority;

    private IUtilityTask? _currentUtilityTask;
    private BehaviorTask? _runningChild;
    private float _reassessmentTimer;
    #endregion

    #region TASK_UPDATES
    public override void Init(Node agent, IBlackboard bb)
    {
        base.Init(agent, bb);

        // Validate that children are the correct type during initialization.
        foreach (var childTask in ChildTasks)
        {
            if (childTask is not IUtilityTask)
            {
                JmoLogger.Error(this, $"Child task '{childTask.Name}' does not implement IUtilityTask and will be ignored.");
            }
        }
    }

    protected override void OnEnter()
    {
        base.OnEnter();
        _reassessmentTimer = 0f; // Assess immediately on first entry.
        SelectBestAction();
    }

    protected override void OnExit()
    {
        base.OnExit();
        // Unsubscribe and exit current child
        if (_runningChild != null)
        {
            _runningChild.TaskStatusChanged -= OnChildStatusChanged;
            _runningChild.Exit();
            _runningChild = null;
        }
        _currentUtilityTask = null;
    }

    protected override void OnProcessPhysics(float delta)
    {
        base.OnProcessPhysics(delta);

        // Don't do anything if the running task is not interruptible.
        if (_currentUtilityTask == null || !_currentUtilityTask.Interruptible)
        {
            return;
        }

        _reassessmentTimer -= delta;
        if (_reassessmentTimer <= 0f)
        {
            _reassessmentTimer = _reassessmentInterval;
            SelectBestAction();
        }
    }
    #endregion

    #region TASK_HELPER
    protected virtual void SelectBestAction()
    {
        var validTasks = ChildTasks.OfType<IUtilityTask>().ToList();
        if (validTasks.Count == 0)
        {
            JmoLogger.Error(this, "No valid children implementing IUtilityTask found.");
            Status = TaskStatus.Failure;
            return;
        }

        // --- Step 1: Cache scores in a single pass (performance fix). ---
        var taskScores = new Dictionary<IUtilityTask, float>();
        float maxScore = -1.0f;
        foreach (var task in validTasks)
        {
            if (task.Consideration == null)
            {
                JmoLogger.Warning(this, $"Utility task '{((Node)task).Name}' has no Consideration assigned — skipping.");
                continue;
            }
            float score = task.Consideration.Evaluate(BB);
            taskScores[task] = score;
            if (score > maxScore)
            {
                maxScore = score;
            }
        }

        // If no action had a score above 0, it means nothing is desirable.
        if (maxScore <= 0)
        {
            Status = TaskStatus.Failure;
            if (_runningChild != null)
            {
                _runningChild.TaskStatusChanged -= OnChildStatusChanged;
                _runningChild.Exit();
                _runningChild = null;
            }
            _currentUtilityTask = null;
            return;
        }

        // --- Step 2: Get all tasks that share the highest score (using cached scores). ---
        var topTasks = taskScores.Where(kvp => kvp.Value >= maxScore).Select(kvp => kvp.Key).ToList();

        // --- Step 3: Use the tie-breaker to select the single best action from the top contenders. ---
        IUtilityTask bestAction;
        if (topTasks.Count == 1)
        {
            bestAction = topTasks[0];
        }
        else
        {
            switch (_tieBreaker)
            {
                case TieBreaker.HighestPriority:
                    bestAction = topTasks.OrderByDescending(t => t.Priority).First();
                    break;
                case TieBreaker.Random:
                    bestAction = topTasks[JmoRng.Rnd.Next(0, topTasks.Count)];
                    break;
                case TieBreaker.FirstInList:
                default:
                    bestAction = topTasks[0];
                    break;
            }
        }

        // --- Step 4: Switch to the best action only if it's not already the running one. ---
        if (_currentUtilityTask != bestAction)
        {
            // Unsubscribe and exit old child
            if (_runningChild != null)
            {
                _runningChild.TaskStatusChanged -= OnChildStatusChanged;
                _runningChild.Exit();
            }

            _currentUtilityTask = bestAction;
            _runningChild = bestAction as BehaviorTask;

            if (_runningChild != null)
            {
                _runningChild.TaskStatusChanged += OnChildStatusChanged;
                _runningChild.Enter();
            }
        }
    }

    private void OnChildStatusChanged(TaskStatus newStatus)
    {
        if (newStatus is TaskStatus.Running or TaskStatus.Fresh) { return; }

        // If the child fails, try to re-select a new best action
        if (newStatus == TaskStatus.Failure)
        {
            SelectBestAction();
            // If still running after re-selection, don't propagate failure
            if (Status == TaskStatus.Running)
            {
                return;
            }
        }

        // Mirror the child's status
        Status = newStatus;
    }
    #endregion
}
