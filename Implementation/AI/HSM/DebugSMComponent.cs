namespace Jmodot.Implementation.AI.HSM;

using System.Collections.Generic;

/// <summary>
/// A visual debugging tool for Hierarchical State Machines. When enabled on a CompoundState,
/// it displays the current state and a history of recent states as text labels in the game world.
/// This component is managed internally by the CompoundState and should not be added manually.
/// </summary>
public partial class DebugSMComponent : Control
{
    private const float LABEL_TIMEOUT = 5.0f;
    private const int MAX_HISTORY = 10;

    private VBoxContainer _labelContainer;
    private Label _currentStateLabel;
    private double _currentStateTime = 0.0;
    private readonly Queue<Label> _historyLabels = new();

    public override void _Ready()
    {
        // The container will automatically stack labels vertically.
        _labelContainer = new VBoxContainer
        {
            Name = "LabelContainer"
        };
        AddChild(_labelContainer);
    }

    public override void _Process(double delta)
    {
        if (Engine.IsEditorHint() || !_currentStateLabel.IsValid()) return;

        _currentStateTime += delta;
        _currentStateLabel.Text = $"> {Name}: {_currentStateTime:n2}";
    }

    /// <summary>
    /// Sets the on-screen position of the debug view using anchors.
    /// </summary>
    public void SetDisplayPosition(CompoundState.DebugViewPosition position)
    {
        // Set anchors to pin the control to a corner of the screen.
        switch (position)
        {
            case CompoundState.DebugViewPosition.TopLeft:
                SetAnchorsPreset(LayoutPreset.TopLeft);
                Position = new Vector2(20, 20); // Add some margin
                break;
            case CompoundState.DebugViewPosition.TopRight:
                SetAnchorsPreset(LayoutPreset.TopRight);
                Position = new Vector2(-20, 20); // Margin
                _labelContainer.Alignment = BoxContainer.AlignmentMode.End;
                break;
            case CompoundState.DebugViewPosition.BottomLeft:
                SetAnchorsPreset(LayoutPreset.BottomLeft);
                Position = new Vector2(20, -20); // Margin
                break;
            case CompoundState.DebugViewPosition.BottomRight:
                SetAnchorsPreset(LayoutPreset.BottomRight);
                Position = new Vector2(-20, -20); // Margin
                _labelContainer.Alignment = BoxContainer.AlignmentMode.End;
                break;
        }
    }

    public void OnEnteredCompoundState(State initialState)
    {
        Name = initialState.GetParent().Name; // Use the CompoundState's name for the title
        CreateCurrentStateLabel(initialState);
    }

    public void OnExitedCompoundState()
    {
        if (_currentStateLabel.IsValid())
        {
            ArchiveLabelAsHistory(_currentStateLabel);
        }
        _currentStateLabel = null;
    }

    public void OnTransitionedState(State oldState, State newState)
    {
        _currentStateTime = 0.0;

        if (_currentStateLabel.IsValid())
        {
            // Move the old "current" label to the history.
            ArchiveLabelAsHistory(_currentStateLabel);
        }

        CreateCurrentStateLabel(newState);
    }

    private void CreateCurrentStateLabel(State currentState)
    {
        _currentStateLabel = new Label
        {
            Name = currentState.Name,
            Text = $"> {Name}: 0.00",
            HorizontalAlignment = _labelContainer.Alignment == BoxContainer.AlignmentMode.End
                                  ? HorizontalAlignment.Right
                                  : HorizontalAlignment.Left
        };
        _labelContainer.AddChild(_currentStateLabel);
    }

    private void ArchiveLabelAsHistory(Label oldLabel)
    {
        oldLabel.Text = $"  - {oldLabel.Name}: {_currentStateTime:n2}"; // Indent and finalize time
        _historyLabels.Enqueue(oldLabel);

        // Prune the history if it gets too long.
        if (_historyLabels.Count > MAX_HISTORY)
        {
            var prunedLabel = _historyLabels.Dequeue();
            FadeOutLabel(prunedLabel, 0f); // Fade immediately
        }

        // Fade out all history labels over time.
        foreach (var label in _historyLabels)
        {
            FadeOutLabel(label, LABEL_TIMEOUT);
        }
    }

    private void FadeOutLabel(Label label, float delay)
    {
        var tween = CreateTween();
        tween.TweenProperty(label, "modulate:a", 0.0f, 1.0f)
             .SetDelay(delay)
             .SetEase(Tween.EaseType.In);
        tween.TweenCallback(Callable.From(label.QueueFree));
    }
}
