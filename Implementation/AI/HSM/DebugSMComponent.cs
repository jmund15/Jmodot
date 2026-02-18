namespace Jmodot.Implementation.AI.HSM;

using System.Collections.Generic;

/// <summary>
/// A visual debugging tool for Hierarchical State Machines. When enabled on a CompoundState,
/// it displays the current active state with elapsed time and a history of recent state transitions.
/// Inherits from DebugAIPanel for proper CanvasLayer rendering, multi-instance stacking,
/// and optional follow-owner positioning.
/// </summary>
[Tool]
public partial class DebugSMComponent : DebugAIPanel
{
    public const int MAX_HISTORY = 10;
    private const float LABEL_TIMEOUT = 5.0f;

    private VBoxContainer? _labelContainer;
    private Label? _currentStateLabel;
    private double _currentStateTime;
    private readonly Queue<Label> _historyLabels = new();

    /// <summary>
    /// The name of the owning CompoundState (for display).
    /// </summary>
    public string OwnerName { get; private set; } = string.Empty;

    /// <summary>
    /// The name of the currently active state, or null if no state is active.
    /// </summary>
    public string? CurrentStateName { get; private set; }

    /// <summary>
    /// The elapsed time in the current state.
    /// </summary>
    public double CurrentStateTime => _currentStateTime;

    /// <summary>
    /// The number of labels currently in the history queue.
    /// </summary>
    public int HistoryCount => _historyLabels.Count;

    public DebugSMComponent()
    {
        PanelSize = new Vector2(250, 300);
    }

    public override void _Ready()
    {
        base._Ready();

        _labelContainer = new VBoxContainer
        {
            Name = "LabelContainer"
        };
        AddChild(_labelContainer);

        Hide();
    }

    public override void _Process(double delta)
    {
        base._Process(delta);
        if (Engine.IsEditorHint()) { return; }

        UpdateTimer(delta);
    }

    /// <summary>
    /// Initializes the debug component with the owning CompoundState's name.
    /// </summary>
    public void Init(string ownerName, Node3D? followTarget = null)
    {
        OwnerName = ownerName;
        if (followTarget != null)
        {
            SetOwnerNode(followTarget);
        }
    }

    /// <summary>
    /// Called when the parent CompoundState enters. Creates the initial current state label.
    /// </summary>
    public void OnEnteredCompoundState(string initialStateName)
    {
        CreateCurrentStateLabel(initialStateName);
        ShowPanel();
    }

    /// <summary>
    /// Overload accepting a State node directly (for backward compatibility with CompoundState).
    /// </summary>
    public void OnEnteredCompoundState(State initialState)
    {
        OnEnteredCompoundState(initialState.Name);
    }

    /// <summary>
    /// Called when the parent CompoundState exits. Archives the current label and hides.
    /// </summary>
    public void OnExitedCompoundState()
    {
        if (_currentStateLabel.IsValid())
        {
            ArchiveLabelAsHistory(_currentStateLabel!);
        }
        _currentStateLabel = null;
        CurrentStateName = null;
        HidePanel();
    }

    /// <summary>
    /// Called when a state transition occurs. Archives the old state and creates a new current label.
    /// </summary>
    public void OnTransitionedState(string oldStateName, string newStateName)
    {
        _currentStateTime = 0.0;

        if (_currentStateLabel.IsValid())
        {
            ArchiveLabelAsHistory(_currentStateLabel!);
        }

        CreateCurrentStateLabel(newStateName);
    }

    /// <summary>
    /// Overload accepting State nodes directly (for backward compatibility with CompoundState).
    /// </summary>
    public void OnTransitionedState(State oldState, State newState)
    {
        OnTransitionedState(oldState.Name, newState.Name);
    }

    /// <summary>
    /// Updates the current state timer. Called from _Process but also exposed for testing.
    /// </summary>
    public void UpdateTimer(double delta)
    {
        if (!_currentStateLabel.IsValid()) { return; }

        _currentStateTime += delta;
        _currentStateLabel!.Text = $"> {CurrentStateName}: {_currentStateTime:n2}";
    }

    private void CreateCurrentStateLabel(string stateName)
    {
        CurrentStateName = stateName;
        _currentStateTime = 0.0;

        var alignment = DisplayPosition is DebugViewPosition.TopRight or DebugViewPosition.BottomRight
            ? HorizontalAlignment.Right
            : HorizontalAlignment.Left;

        _currentStateLabel = new Label
        {
            Name = stateName,
            Text = $"> {stateName}: 0.00",
            HorizontalAlignment = alignment
        };

        _labelContainer?.AddChild(_currentStateLabel);
    }

    private void ArchiveLabelAsHistory(Label oldLabel)
    {
        oldLabel.Text = $"  - {oldLabel.Name}: {_currentStateTime:n2}";
        _historyLabels.Enqueue(oldLabel);

        // Prune if history exceeds max
        while (_historyLabels.Count > MAX_HISTORY)
        {
            var prunedLabel = _historyLabels.Dequeue();
            prunedLabel.QueueFree();
        }

        // Only fade the newly archived label (NOT all history — fixes tween stacking bug)
        FadeOutLabel(oldLabel, LABEL_TIMEOUT);
    }

    private void FadeOutLabel(Label label, float delay)
    {
        var tween = CreateManagedTween(label);
        tween.TweenProperty(label, "modulate:a", 0.0f, 1.0f)
             .SetDelay(delay)
             .SetEase(Tween.EaseType.In);
        tween.TweenCallback(Callable.From(label.QueueFree));
    }

    protected override void Cleanup()
    {
        base.Cleanup();

        // Free all history labels
        while (_historyLabels.Count > 0)
        {
            var label = _historyLabels.Dequeue();
            if (label.IsValid())
            {
                label.QueueFree();
            }
        }

        if (_currentStateLabel.IsValid())
        {
            _currentStateLabel!.QueueFree();
        }
        _currentStateLabel = null;
        CurrentStateName = null;
    }
}
