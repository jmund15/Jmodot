namespace Jmodot.Implementation.AI.HSM;

using System;
using System.Collections.Generic;
using System.Linq;
using BehaviorTree;
using Shared;

/// <summary>
/// Self-contained debug panel for HSM overview. Shows all states at a glance
/// with expandable/collapsible per-state BT visualizations and a scrollable log.
/// Always embedded inside AIDebugDashboard — never standalone.
/// </summary>
public partial class DebugHSMOverviewPanel : Control
{
    private const int MaxLogEntries = 50;
    private const float RowHeightPx = 26f;
    private const float BTPaddingPx = 12f;

    private CompoundState? _compoundState;
    private VBoxContainer _layout = null!;
    private VBoxContainer _stateSection = null!;
    private ScrollContainer _logScroll = null!;
    private VBoxContainer _logSection = null!;
    private ulong _startTime;

    private readonly Queue<string> _logBuffer = new();
    private readonly Dictionary<State, StateRowData> _stateRows = new();
    private readonly List<Action> _unsubscribeActions = new();
    private State? _activeState;
    private double _activeStateTime;
    private bool _subscribed;

    private class StateRowData
    {
        public HBoxContainer Header { get; set; } = null!;
        public Button ToggleButton { get; set; } = null!;
        public Label NameLabel { get; set; } = null!;
        public ColorRect ActiveIndicator { get; set; } = null!;
        public Label TimerLabel { get; set; } = null!;
        public VBoxContainer BTContainer { get; set; } = null!;
        public DebugBTComponent? BTComponent { get; set; }
        public bool IsExpanded { get; set; }
        public double ElapsedTime { get; set; }
    }

    /// <summary>Number of state rows currently displayed.</summary>
    public int StateRowCount => _stateRows.Count;

    /// <summary>Number of log entries in the buffer.</summary>
    public int LogEntryCount => _logBuffer.Count;

    /// <summary>
    /// Initializes the panel with its owning CompoundState.
    /// Builds the layout, populates state rows, and subscribes to signals.
    /// </summary>
    public void Init(CompoundState compoundState)
    {
        _compoundState = compoundState;
        _startTime = Time.GetTicksMsec();
        BuildLayout();
        PopulateStateRows();
        Subscribe();

        // If already entered, set initial highlight
        if (_compoundState.PrimarySubState.IsValid())
        {
            SetActiveState(_compoundState.PrimarySubState);
        }
    }

    /// <summary>
    /// Per-frame update. Increments the active state timer.
    /// Called by CompoundState.UpdateDebugContent(delta).
    /// </summary>
    public void Refresh(double delta)
    {
        if (_activeState == null) { return; }

        _activeStateTime += delta;
        if (_stateRows.TryGetValue(_activeState, out var row))
        {
            row.ElapsedTime = _activeStateTime;
            row.TimerLabel.Text = _activeStateTime.ToString("F2");
        }
    }

    #region Layout

    private void BuildLayout()
    {
        _layout = new VBoxContainer
        {
            Name = "HSMOverviewLayout",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        AddChild(_layout);
        _layout.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        // State overview header
        var stateHeader = new Label
        {
            Text = "State Overview",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        _layout.AddChild(stateHeader);

        // State section (scrollable)
        var stateScroll = new ScrollContainer
        {
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        _layout.AddChild(stateScroll);

        _stateSection = new VBoxContainer
        {
            Name = "StateSection",
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        stateScroll.AddChild(_stateSection);

        _layout.AddChild(new HSeparator());

        // Log section
        var logHeader = new Label
        {
            Text = "Behavior Log",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        _layout.AddChild(logHeader);

        _logScroll = new ScrollContainer
        {
            CustomMinimumSize = new Vector2(0, 80),
            SizeFlagsVertical = SizeFlags.ShrinkEnd
        };
        _layout.AddChild(_logScroll);

        _logSection = new VBoxContainer { Name = "LogSection" };
        _logScroll.AddChild(_logSection);
    }

    private void PopulateStateRows()
    {
        if (_compoundState == null) { return; }

        foreach (var state in _compoundState.FiniteSubStates.Keys)
        {
            CreateStateRow(state);
        }
    }

    private void CreateStateRow(State state)
    {
        var rowContainer = new VBoxContainer
        {
            Name = $"Row_{state.Name}",
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };

        // Header row
        var header = new HBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };

        // Toggle button (only visible if state has BT)
        var toggleButton = new Button
        {
            Text = "▶",
            CustomMinimumSize = new Vector2(24, 24),
            FocusMode = FocusModeEnum.None
        };

        // Name label
        var nameLabel = new Label
        {
            Text = state.Name,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };

        // Active indicator (colored dot)
        var activeIndicator = new ColorRect
        {
            CustomMinimumSize = new Vector2(12, 12),
            Color = new Color(0.3f, 0.3f, 0.3f, 0.5f) // Dim by default
        };

        // Timer label
        var timerLabel = new Label
        {
            Text = "0.00",
            CustomMinimumSize = new Vector2(50, 0),
            HorizontalAlignment = HorizontalAlignment.Right
        };

        header.AddChild(toggleButton);
        header.AddChild(nameLabel);
        header.AddChild(activeIndicator);
        header.AddChild(timerLabel);

        rowContainer.AddChild(header);

        // BT container (holds embedded DebugBTComponent)
        var btContainer = new VBoxContainer
        {
            Name = $"BT_{state.Name}",
            Visible = false,
            SizeFlagsVertical = SizeFlags.ShrinkBegin
        };
        rowContainer.AddChild(btContainer);

        var rowData = new StateRowData
        {
            Header = header,
            ToggleButton = toggleButton,
            NameLabel = nameLabel,
            ActiveIndicator = activeIndicator,
            TimerLabel = timerLabel,
            BTContainer = btContainer,
            IsExpanded = false,
            ElapsedTime = 0
        };

        // Try to find and embed BT
        if (state.TryGetFirstChildOfType<BehaviorTree>(out var bt, false) && bt?.RootTask != null)
        {
            var debugBT = new DebugBTComponent { EmbeddedMode = true };
            debugBT.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            debugBT.Init(bt.RootTask, bt.Name);
            debugBT.BuildTreeView();
            debugBT.SetEmbeddedHeight(CalculateBTHeight(debugBT.MappedTaskCount));
            btContainer.AddChild(debugBT);
            rowData.BTComponent = debugBT;

            // Subscribe to BT signals
            SubscribeToBT(bt, debugBT);

            // If BT is enabled, show it
            if (bt.Enabled)
            {
                debugBT.OnTreeEnabled();
            }
        }
        else
        {
            // No BT — hide toggle button
            toggleButton.Visible = false;
        }

        // Wire toggle button
        toggleButton.Pressed += () => ToggleStateExpansion(state);

        _stateRows[state] = rowData;
        _stateSection.AddChild(rowContainer);
    }

    private static float CalculateBTHeight(int taskCount)
    {
        return taskCount * RowHeightPx + BTPaddingPx;
    }

    #endregion

    #region State Management

    private void SetActiveState(State state)
    {
        // Clear old highlight (but preserve BT expansion state)
        if (_activeState != null && _stateRows.TryGetValue(_activeState, out var oldRow))
        {
            oldRow.ActiveIndicator.Color = new Color(0.3f, 0.3f, 0.3f, 0.5f);
            oldRow.Header.SelfModulate = Colors.White;
        }

        // Freeze old state timer at its last value
        _activeState = state;
        _activeStateTime = 0;

        // Set new highlight
        if (_stateRows.TryGetValue(state, out var newRow))
        {
            newRow.ActiveIndicator.Color = new Color(1f, 0.9f, 0.2f, 1f); // Yellow
            newRow.Header.SelfModulate = new Color(1f, 1f, 0.8f);
            newRow.ElapsedTime = 0;
            newRow.TimerLabel.Text = "0.00";

            // Auto-expand new state's BT
            if (newRow.BTComponent != null)
            {
                newRow.IsExpanded = true;
                newRow.BTContainer.Visible = true;
                newRow.ToggleButton.Text = "▼";
            }
        }
    }

    private void ToggleStateExpansion(State state)
    {
        if (!_stateRows.TryGetValue(state, out var row)) { return; }
        if (row.BTComponent == null) { return; }

        row.IsExpanded = !row.IsExpanded;
        row.BTContainer.Visible = row.IsExpanded;
        row.ToggleButton.Text = row.IsExpanded ? "▼" : "▶";
    }

    #endregion

    #region Signal Subscriptions

    private void Subscribe()
    {
        if (_compoundState == null || _subscribed) { return; }

        _compoundState.EnteredCompoundState += OnEnteredCompoundState;
        _compoundState.TransitionedSubState += OnTransitionedSubState;
        _compoundState.ExitedCompoundState += OnExitedCompoundState;
        _subscribed = true;

        TreeExiting += Unsubscribe;
    }

    private void Unsubscribe()
    {
        if (_compoundState == null || !_subscribed) { return; }

        _compoundState.EnteredCompoundState -= OnEnteredCompoundState;
        _compoundState.TransitionedSubState -= OnTransitionedSubState;
        _compoundState.ExitedCompoundState -= OnExitedCompoundState;

        // Unsubscribe all per-BT handlers
        foreach (var unsub in _unsubscribeActions)
        {
            unsub();
        }
        _unsubscribeActions.Clear();

        _subscribed = false;
    }

    private void SubscribeToBT(BehaviorTree bt, DebugBTComponent debugBT)
    {
        // TreeFinishedLoop → log entry
        BehaviorTree.TreeFinishedLoopEventHandler finishedHandler = (status) =>
        {
            AddLogEntry($"{bt.Name}: {status}");
        };
        bt.TreeFinishedLoop += finishedHandler;

        // TreeEnabled/Disabled/Reset → forward to embedded component
        BehaviorTree.TreeEnabledEventHandler enabledHandler = debugBT.OnTreeEnabled;
        BehaviorTree.TreeDisabledEventHandler disabledHandler = debugBT.OnTreeDisabled;
        BehaviorTree.TreeResetEventHandler resetHandler = debugBT.OnTreeReset;

        bt.TreeEnabled += enabledHandler;
        bt.TreeDisabled += disabledHandler;
        bt.TreeReset += resetHandler;

        _unsubscribeActions.Add(() =>
        {
            if (bt.IsValid())
            {
                bt.TreeFinishedLoop -= finishedHandler;
                bt.TreeEnabled -= enabledHandler;
                bt.TreeDisabled -= disabledHandler;
                bt.TreeReset -= resetHandler;
            }
        });
    }

    private void OnEnteredCompoundState()
    {
        if (_compoundState?.PrimarySubState.IsValid() == true)
        {
            SetActiveState(_compoundState.PrimarySubState);
            AddLogEntry($"Entered: {_compoundState.PrimarySubState.Name}");
        }
    }

    private void OnTransitionedSubState(State oldState, State newState)
    {
        SetActiveState(newState);
        AddLogEntry($"{oldState.Name} → {newState.Name}");
    }

    private void OnExitedCompoundState()
    {
        // Clear all highlights
        if (_activeState != null && _stateRows.TryGetValue(_activeState, out var row))
        {
            row.ActiveIndicator.Color = new Color(0.3f, 0.3f, 0.3f, 0.5f);
            row.Header.SelfModulate = Colors.White;
        }
        _activeState = null;
        AddLogEntry("Exited");
    }

    #endregion

    #region Log

    private void AddLogEntry(string message)
    {
        var elapsed = (Time.GetTicksMsec() - _startTime) / 1000f;
        var entry = $"[+{elapsed:F2}] {message}";

        _logBuffer.Enqueue(entry);
        while (_logBuffer.Count > MaxLogEntries)
        {
            _logBuffer.Dequeue();
        }

        RebuildLogDisplay();
    }

    private void RebuildLogDisplay()
    {
        // Check if user is at bottom BEFORE rebuilding
        var scrollBar = _logScroll.GetVScrollBar();
        bool wasAtBottom = scrollBar.Value >= scrollBar.MaxValue - _logScroll.Size.Y - 10;

        foreach (var child in _logSection.GetChildren())
        {
            _logSection.RemoveChild(child);
            child.Free();
        }

        foreach (var entry in _logBuffer)
        {
            var label = new Label { Text = entry };
            label.AddThemeColorOverride("font_color", new Color(0.7f, 0.7f, 0.7f));
            _logSection.AddChild(label);
        }

        // Only auto-scroll if user was already at bottom
        if (wasAtBottom && IsInsideTree())
        {
            _logScroll.CallDeferred("set_v_scroll", (int)scrollBar.MaxValue);
        }
    }

    #endregion

    #region Test Helpers
#if TOOLS
    internal string GetStateRowName(int index)
    {
        if (index < 0 || index >= _stateRows.Count) { return string.Empty; }
        return _stateRows.ElementAt(index).Value.NameLabel.Text;
    }

    internal bool IsStateHighlighted(string stateName)
    {
        var row = FindRow(stateName);
        if (row == null) { return false; }
        return row.ActiveIndicator.Color.A > 0.9f; // Active = full alpha yellow
    }

    internal bool IsStateExpanded(string stateName)
    {
        var row = FindRow(stateName);
        return row?.IsExpanded ?? false;
    }

    internal string GetActiveStateName()
    {
        return _activeState?.Name ?? string.Empty;
    }

    internal string GetLogEntryText(int index)
    {
        if (index < 0 || index >= _logBuffer.Count) { return string.Empty; }
        return _logBuffer.ElementAt(index);
    }

    internal string GetStateTimerText(string stateName)
    {
        var row = FindRow(stateName);
        return row?.TimerLabel.Text ?? string.Empty;
    }

    internal void SimulateToggle(string stateName)
    {
        foreach (var kvp in _stateRows)
        {
            if (kvp.Key.Name == stateName)
            {
                ToggleStateExpansion(kvp.Key);
                return;
            }
        }
    }

    internal void SimulateCleanup() => Unsubscribe();

    internal ScrollContainer GetLogScroll() => _logScroll;

    internal float GetBTMinHeight(string stateName)
    {
        var row = FindRow(stateName);
        return row?.BTComponent?.CustomMinimumSize.Y ?? -1f;
    }

    private StateRowData? FindRow(string stateName)
    {
        foreach (var kvp in _stateRows)
        {
            if (kvp.Key.Name == stateName)
            {
                return kvp.Value;
            }
        }
        return null;
    }
#endif
    #endregion
}
