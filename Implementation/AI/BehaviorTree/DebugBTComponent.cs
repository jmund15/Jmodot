namespace Jmodot.Implementation.AI.BehaviorTree;

using System.Collections.Generic;
using System.Linq;
using Core.AI;
using Tasks;

/// <summary>
/// A visual debugging tool for Behavior Trees. When enabled, it creates a Tree UI
/// that mirrors the BT's structure and visualizes the real-time status of each task.
/// Inherits from DebugAIPanel for proper CanvasLayer rendering, multi-instance stacking,
/// and optional follow-owner positioning.
///
/// Uses composition (owns a Tree child) rather than inheritance to keep the panel
/// lifecycle separate from the Tree control lifecycle.
/// </summary>
[Tool]
public partial class DebugBTComponent : DebugAIPanel
{
    private const float CompletionHoldDuration = 0.15f;
    private const float CompletionFadeDuration = 0.6f;

    private readonly Color _baseBgColor = new(Colors.Black, 0.05f);
    private readonly Color _runningColor = new(Colors.Yellow, 0.25f);
    private readonly Color _successColor = new(Colors.Green, 0.25f);
    private readonly Color _failureColor = new(Colors.Red, 0.25f);

    private Tree? _treeUI;
    private BehaviorTask? _rootTask;

    private readonly Dictionary<BehaviorTask, TreeItem> _taskToItem = new();
    private readonly Dictionary<TreeItem, BehaviorTask> _itemToTask = new();
    private readonly Dictionary<BehaviorTask, float> _taskRunTime = new();
    private readonly HashSet<BehaviorTask> _runningTasks = new();
    private readonly Dictionary<BehaviorTask, Callable> _signalCallables = new();

    /// <summary>Whether the internal Tree UI control has been created.</summary>
    public bool HasTreeUI => _treeUI != null;

    /// <summary>Number of BehaviorTasks mapped to TreeItems.</summary>
    public int MappedTaskCount => _taskToItem.Count;

    /// <summary>Number of currently running tasks.</summary>
    public int RunningTaskCount => _runningTasks.Count;

    public DebugBTComponent()
    {
        PanelSize = new Vector2(350, 648);
    }

    public override void _Ready()
    {
        base._Ready();
        EnsureTreeUI();
        if (!EmbeddedMode) { Hide(); }
    }

    public override void _Process(double delta)
    {
        base._Process(delta);
        if (Engine.IsEditorHint()) { return; }

        // Snapshot to prevent InvalidOperationException if signal callbacks modify _runningTasks mid-iteration
        foreach (var runningTask in _runningTasks.ToArray())
        {
            if (!_taskToItem.TryGetValue(runningTask, out var item)) { continue; }

            _taskRunTime[runningTask] += (float)delta;
            item.SetText(1, _taskRunTime[runningTask].ToString("n2"));
        }
    }

    #region Public API

    /// <summary>
    /// Initializes the debug component with the root task to visualize.
    /// </summary>
    public void Init(BehaviorTask rootTask, string btName, Node3D? followTarget = null)
    {
        _rootTask = rootTask;
        Name = $"{btName}Debugger";

        if (followTarget != null)
        {
            SetOwnerNode(followTarget);
        }
    }

    /// <summary>
    /// Builds the visual tree from the current root task structure.
    /// Must be called after Init() and after the BT's tasks are fully initialized.
    /// </summary>
    public void BuildTreeView()
    {
        if (_rootTask == null) { return; }
        EnsureTreeUI();

        _treeUI.Clear();
        _taskToItem.Clear();
        _itemToTask.Clear();
        _taskRunTime.Clear();
        _runningTasks.Clear();
        DisconnectAllSignals();

        var rootItem = _treeUI.CreateItem();
        rootItem.SetText(0, _rootTask.Name);
        _treeUI.HideRoot = false;

        MapAndSubscribe(_rootTask, rootItem);
        CreateBranchesRecursive(rootItem, _rootTask);
    }

    /// <summary>
    /// Checks whether a specific task has a corresponding TreeItem in the visualization.
    /// </summary>
    public bool HasMappedTask(BehaviorTask task)
    {
        return _taskToItem.ContainsKey(task);
    }

    /// <summary>
    /// Checks whether a task is currently tracked as running.
    /// </summary>
    public bool IsTaskRunning(BehaviorTask task)
    {
        return _runningTasks.Contains(task);
    }

    /// <summary>
    /// Sets the embedded height on both this component and its internal Tree control.
    /// Must be called after BuildTreeView() so the Tree's min size prevents internal scrolling.
    /// </summary>
    public void SetEmbeddedHeight(float height)
    {
        CustomMinimumSize = new Vector2(0, height);
        if (_treeUI != null)
        {
            _treeUI.CustomMinimumSize = new Vector2(0, height);
        }
    }

    /// <summary>
    /// Called when the owning BehaviorTree is enabled.
    /// In embedded mode, restores full opacity instead of toggling visibility.
    /// </summary>
    public void OnTreeEnabled()
    {
        if (EmbeddedMode)
        {
            Modulate = Colors.White;
            return;
        }
        ShowPanel();
    }

    /// <summary>
    /// Called when the owning BehaviorTree is disabled.
    /// In embedded mode, dims the panel instead of hiding it so the tree structure remains inspectable.
    /// </summary>
    public void OnTreeDisabled()
    {
        if (EmbeddedMode)
        {
            Modulate = new Color(1f, 1f, 1f, 0.4f);
            return;
        }
        HidePanel();
    }

    /// <summary>
    /// Called when the BehaviorTree resets its loop. Clears all running state.
    /// </summary>
    public void OnTreeReset()
    {
        _runningTasks.Clear();

        foreach (var kvp in _taskToItem)
        {
            _taskRunTime[kvp.Key] = 0.0f;
            kvp.Value.SetText(1, "0.00");
            KillManagedTween(kvp.Key);
            SetItemColor(kvp.Value, _baseBgColor);
        }
    }

    #endregion

    #region Tree Building

    /// <summary>
    /// Lazily creates the Tree UI if it hasn't been initialized yet.
    /// Handles the case where _Ready() hasn't fired (e.g., node not in scene tree).
    /// </summary>
    private void EnsureTreeUI()
    {
        if (_treeUI != null) { return; }

        _treeUI = new Tree
        {
            Name = "BTTreeUI",
            Columns = 2,
            CustomMinimumSize = EmbeddedMode ? Vector2.Zero : PanelSize,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        _treeUI.SetColumnTitle(0, "Task Name");
        _treeUI.SetColumnTitle(1, "Time");
        _treeUI.SetColumnExpand(0, true);
        _treeUI.SetColumnExpandRatio(0, 4);
        _treeUI.SetColumnExpand(1, true);
        _treeUI.SetColumnExpandRatio(1, 1);
        _treeUI.SetColumnCustomMinimumWidth(1, 60);

        AddChild(_treeUI);

        if (EmbeddedMode)
        {
            _treeUI.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            _treeUI.ScrollHorizontalEnabled = false;
            _treeUI.ScrollVerticalEnabled = false;
        }
    }

    private void CreateBranchesRecursive(TreeItem parentItem, BehaviorTask parentTask)
    {
        foreach (var child in parentTask.GetChildren())
        {
            if (child is not BehaviorTask btChild) { continue; }

            var branchItem = _treeUI!.CreateItem(parentItem);
            branchItem.SetText(0, btChild.Name);
            branchItem.SetTooltipText(0, btChild.GetClass());

            MapAndSubscribe(btChild, branchItem);

            if (btChild.GetChildCount() > 0)
            {
                CreateBranchesRecursive(branchItem, btChild);
            }
        }
    }

    private void MapAndSubscribe(BehaviorTask task, TreeItem item)
    {
        _taskToItem[task] = item;
        _itemToTask[item] = task;
        _taskRunTime[task] = 0.0f;
        item.SetText(1, "0.00");
        SetItemColor(item, _baseBgColor);

        // Store the Callable so we can properly disconnect later
        var callable = Callable.From((long newStatus) => OnTaskStatusChange(task, (TaskStatus)newStatus));
        _signalCallables[task] = callable;
        task.Connect(BehaviorTask.SignalName.TaskStatusChanged, callable);
    }

    #endregion

    #region Status Handling

    private void OnTaskStatusChange(BehaviorTask task, TaskStatus newStatus)
    {
        if (!_taskToItem.TryGetValue(task, out var item)) { return; }

        bool isStale = task.Status != newStatus;

        if (isStale)
        {
            if (newStatus is TaskStatus.Success or TaskStatus.Failure)
            {
                var flashColor = newStatus == TaskStatus.Success ? _successColor : _failureColor;
                ShowCompletionFlash(task, item, flashColor, ColorForStatus(task.Status));
            }
            return;
        }

        switch (newStatus)
        {
            case TaskStatus.Running:
                KillManagedTween(task);
                if (_runningTasks.Add(task))
                {
                    _taskRunTime[task] = 0.0f;
                    item.SetText(1, "0.00");
                }
                SetItemColor(item, _runningColor);
                break;
            case TaskStatus.Success:
                _runningTasks.Remove(task);
                ShowCompletionFlash(task, item, _successColor, _baseBgColor);
                break;
            case TaskStatus.Failure:
                _runningTasks.Remove(task);
                ShowCompletionFlash(task, item, _failureColor, _baseBgColor);
                break;
            case TaskStatus.Fresh:
                _runningTasks.Remove(task);
                KillManagedTween(task);
                SetItemColor(item, _baseBgColor);
                break;
        }
    }

    private Color ColorForStatus(TaskStatus status) => status switch
    {
        TaskStatus.Running => _runningColor,
        TaskStatus.Success => _successColor,
        TaskStatus.Failure => _failureColor,
        _ => _baseBgColor
    };

    private void ShowCompletionFlash(BehaviorTask task, TreeItem item, Color flashColor, Color restoreColor)
    {
        SetItemColor(item, flashColor);
        if (!IsInsideTree()) { return; }
        var tween = CreateManagedTween(task);
        tween.TweenMethod(
            Callable.From((Color c) => { if (IsInstanceValid(item)) { SetItemColor(item, c); } }),
            flashColor,
            restoreColor,
            CompletionFadeDuration
        ).SetDelay(CompletionHoldDuration);
    }

    #endregion

    #region Visual Helpers

    private void SetItemColor(TreeItem item, Color color)
    {
        item.SetCustomBgColor(0, color);
        item.SetCustomBgColor(1, color);
    }

    #endregion

    #region Test Helpers
#if TOOLS
    internal Color GetTaskColor(BehaviorTask task)
    {
        if (!_taskToItem.TryGetValue(task, out var item)) { return default; }
        return item.GetCustomBgColor(0);
    }

    internal string GetTaskTimerText(BehaviorTask task)
    {
        if (!_taskToItem.TryGetValue(task, out var item)) { return string.Empty; }
        return item.GetText(1);
    }

    internal float GetTaskRunTime(BehaviorTask task)
    {
        return _taskRunTime.TryGetValue(task, out var time) ? time : -1f;
    }

    internal void SimulateStaleSignal(BehaviorTask task, TaskStatus staleStatus)
    {
        OnTaskStatusChange(task, staleStatus);
    }
#endif
    #endregion

    #region Cleanup

    private void DisconnectAllSignals()
    {
        foreach (var kvp in _signalCallables)
        {
            if (kvp.Key.IsValid() && kvp.Key.IsConnected(BehaviorTask.SignalName.TaskStatusChanged, kvp.Value))
            {
                kvp.Key.Disconnect(BehaviorTask.SignalName.TaskStatusChanged, kvp.Value);
            }
        }
        _signalCallables.Clear();
    }

    protected override void Cleanup()
    {
        DisconnectAllSignals();
        _runningTasks.Clear();
        _taskToItem.Clear();
        _itemToTask.Clear();
        _taskRunTime.Clear();
        base.Cleanup();
    }

    #endregion
}
