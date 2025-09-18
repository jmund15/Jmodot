namespace Jmodot.Implementation.AI.BehaviorTree;

using System.Collections.Generic;
using System.Linq;
using Core.AI;
using Tasks;

/// <summary>
/// A visual debugging tool for Behavior Trees. When enabled on a BehaviorTree, it creates a
/// Tree UI that mirrors the BT's structure and visualizes the real-time status of each task.
/// This component is managed internally by the BehaviorTree and should not be added manually.
/// </summary>
[Tool]
public partial class DebugBTComponent : Tree
{
    private BehaviorTree _bt;
    private Vector2 _size = new Vector2(350, 648);

    private readonly Color _baseBgColor = new(Colors.Black, 0.05f);
    private readonly Color _runningColor = new(Colors.Yellow, 0.25f);
    private readonly Color _successColor = new(Colors.Green, 0.25f);
    private readonly Color _failureColor = new(Colors.Red, 0.25f);

    private Dictionary<BehaviorTask, TreeItem> _taskToItemMap = new();
    private Dictionary<BehaviorTask, float> _taskRunTime = new();
    private List<BehaviorTask> _runningTasks = new();

    public override void _Ready()
    {
        // Basic setup for the Tree UI control.
        Columns = 2;
        SetColumnTitle(0, "Task Name");
        SetColumnTitle(1, "Time");
        SetColumnExpand(0, true);
        SetColumnExpandRatio(0, 4);
        SetColumnExpand(1, true);
        SetColumnExpandRatio(1, 1);
        SetColumnCustomMinimumWidth(1, 60);

        // Hide until the tree is actually entered.
        Hide();
    }

    /// <summary>
    /// Initializes the debug component, connecting it to the target BehaviorTree.
    /// </summary>
    /// <param name="treeToDebug">The BehaviorTree instance to visualize.</param>
    public void Init(BehaviorTree treeToDebug)
    {
        _bt = treeToDebug;
        _bt.TreeInitialized += OnTreeInitialized;
        _bt.TreeEnabled += OnTreeEntered;
        _bt.TreeDisabled += OnTreeExited;
        _bt.TreeReset += OnTreeReset;
    }

    public override void _Process(double delta)
    {
        if (Engine.IsEditorHint())
        {
            Size = _size;
            return;
        }

        // Update timers for all currently running tasks.
        foreach (var runningTask in _runningTasks)
        {
            if (_taskToItemMap.TryGetValue(runningTask, out var item))
            {
                _taskRunTime[runningTask] += (float)delta;
                item.SetText(1, _taskRunTime[runningTask].ToString("n2"));
            }
        }
    }

    /// <summary>
    /// Sets the on-screen position and size of the debug view using anchors.
    /// </summary>
    public void SetDisplayPosition(BehaviorTree.DebugViewPosition position)
    {
        Size = _size;
        switch (position)
        {
            case BehaviorTree.DebugViewPosition.TopLeft:
                SetAnchorsPreset(LayoutPreset.TopLeft);
                Position = new Vector2(10, 10);
                break;
            case BehaviorTree.DebugViewPosition.TopRight:
                SetAnchorsPreset(LayoutPreset.TopRight);
                Position = new Vector2(-10, 10);
                break;
            case BehaviorTree.DebugViewPosition.BottomLeft:
                SetAnchorsPreset(LayoutPreset.BottomLeft);
                Position = new Vector2(10, -10);
                break;
            case BehaviorTree.DebugViewPosition.BottomRight:
                SetAnchorsPreset(LayoutPreset.BottomRight);
                Position = new Vector2(-10, -10);
                break;
        }
    }
    private void OnTreeInitialized()
    {
        if (!_bt.RootTask.IsValid()) return;

        Name = $"{_bt.Name}Debugger";
        var rootItem = CreateItem();
        rootItem.SetText(0, _bt.RootTask.Name);
        HideRoot = false;

        MapAndSubscribe(_bt.RootTask, rootItem);
        CreateBranchesRecursive(rootItem, _bt.RootTask);
    }

    private void CreateBranchesRecursive(TreeItem parentItem, BehaviorTask parentTask)
    {
        foreach (var childTask in parentTask.GetChildren())
        {
            if (childTask is not BehaviorTask btChild) continue;

            var branchItem = CreateItem(parentItem);
            branchItem.SetText(0, btChild.Name);
            branchItem.SetTooltipText(0, btChild.GetClass());

            MapAndSubscribe(btChild, branchItem);

            if (btChild.GetChildCount() > 0)
            {
                CreateBranchesRecursive(branchItem, btChild);
            }
        }
    }

    /// <summary>
    /// Helper to centralize mapping a task to its UI item and subscribing to its status changes.
    /// </summary>
    private void MapAndSubscribe(BehaviorTask task, TreeItem item)
    {
        _taskToItemMap[task] = item;
        _taskRunTime[task] = 0.0f;
        item.SetText(1, "0.00");
        item.SetCustomBgColor(0, _baseBgColor);
        item.SetCustomBgColor(1, _baseBgColor);
        task.TaskStatusChanged += (newStatus) => OnTaskStatusChange(task, newStatus);
    }

    private void OnTaskStatusChange(BehaviorTask task, TaskStatus newStatus)
    {
        if (!_taskToItemMap.TryGetValue(task, out var item)) return;

        // Reset timer and remove from running list when a task stops running.
        if (newStatus != TaskStatus.RUNNING && _runningTasks.Contains(task))
        {
            _runningTasks.Remove(task);
            _taskRunTime[task] = 0.0f;
             item.SetText(1, "0.00");
        }

        switch (newStatus)
        {
            case TaskStatus.RUNNING:
                if (!_runningTasks.Contains(task))
                {
                    _runningTasks.Add(task);
                    _taskRunTime[task] = 0.0f; // Reset time when it starts running.
                }
                SetItemColor(item, _runningColor);
                break;
            case TaskStatus.SUCCESS:
                FlashItemColor(item, _successColor);
                break;
            case TaskStatus.FAILURE:
                FlashItemColor(item, _failureColor);
                break;
            case TaskStatus.FRESH:
                SetItemColor(item, _baseBgColor);
                break;
        }
    }

    private void OnTreeEntered() => Show();
    private void OnTreeExited() => Hide();

    private void OnTreeReset()
    {
        // When the tree resets, set all non-running tasks back to the base color.
        foreach (var kvp in _taskToItemMap)
        {
            if (kvp.Key.Status != TaskStatus.RUNNING)
            {
                SetItemColor(kvp.Value, _baseBgColor);
                if (_taskRunTime.ContainsKey(kvp.Key))
                {
                     _taskRunTime[kvp.Key] = 0.0f;
                     kvp.Value.SetText(1, "0.00");
                }
            }
        }
    }

    private void SetItemColor(TreeItem item, Color color)
    {
        item.SetCustomBgColor(0, color);
        item.SetCustomBgColor(1, color);
    }

    private void FlashItemColor(TreeItem item, Color flashColor)
    {
        SetItemColor(item, flashColor);
        var tween = CreateTween();
        // After a short delay, tween the item's color back to its current "correct" color
        // based on its status (which will be base for success/fail, or running for a parent).
        tween.TweenCallback(Callable.From(() =>
        {
            // Find the task associated with this item to check its current status
            var task = _taskToItemMap.FirstOrDefault(x => x.Value == item).Key;
            if (task != null)
            {
               var targetColor = task.Status == TaskStatus.RUNNING ? _runningColor : _baseBgColor;
               SetItemColor(item, targetColor);
            }
        })).SetDelay(0.5f);
    }
}
