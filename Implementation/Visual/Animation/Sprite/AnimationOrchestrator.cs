namespace Jmodot.Implementation.Visual.Animation.Sprite;

using System;
using Core.Movement;
using Core.Visual.Animation.Sprite;
using Godot;
using Godot.Collections;
using Shared;

/// <summary>
/// Coordinates the high-level animation state (e.g. "run_left").
/// Combines a Base Name (State) with a Direction Suffix.
/// </summary>
[GlobalClass]
public partial class AnimationOrchestrator : Node, IAnimComponent
{
    [Export] private Node _targetAnimatorNode = null!;
    [Export] public string DirectionSuffixSeparator { get; set; } = "_";

    // Helper resource to map Vector3 -> "left", "down_right", etc.
    // If null, direction logic is skipped.
    [Export] public DirectionSet3D DirectionSet { get; set; } = null!;
    [Export] public Dictionary<Vector3, string> DirectionLabels { get; set; } = new();

    private IAnimComponent _targetAnimator = null!;
    private StringName _baseAnimName = "idle";
    private string _currentDirectionLabel = "down";

    public event Action<StringName> AnimStarted = delegate { };
    public event Action<StringName> AnimFinished = delegate { };

    public override void _Ready()
    {
        _targetAnimator = _targetAnimatorNode as IAnimComponent;
        if (_targetAnimator == null)
        {
            GD.PrintErr($"Orchestrator '{Name}': Target is not an IAnimComponent.");
            SetProcess(false);
            return;
        }

        // Forward events
        _targetAnimator.AnimStarted += n => AnimStarted?.Invoke(n);
        _targetAnimator.AnimFinished += n => AnimFinished?.Invoke(n);
    }

    /// <summary>
    /// Updates the direction. Triggers a smooth update to preserve animation time.
    /// </summary>
    public void SetDirection(Vector3 direction)
    {
        if (DirectionSet == null || direction.IsZeroApprox())
        {
            return;
        }

        var closestDir = DirectionSet.GetClosestDirection(direction);
        if (!DirectionLabels.TryGetValue(closestDir, out var newLabel))
        {
            JmoLogger.Error(this, $"Direction Set '{DirectionSet.ResourceName}' does not contain direction '{closestDir}");
            return;
        }

        if (newLabel != _currentDirectionLabel)
        {
            _currentDirectionLabel = newLabel;
            UpdateInternal(forceReset: false);
        }
    }

    /// <summary>
    /// Sets the base state (e.g., "run", "attack"). Triggers a hard reset of the animation.
    /// </summary>
    public void StartAnim(StringName baseName)
    {
        _baseAnimName = baseName;
        UpdateInternal(forceReset: true);
    }

    private void UpdateInternal(bool forceReset)
    {
        var finalName = BuildFinalName();

        if (forceReset)
        {
            _targetAnimator.StartAnim(finalName);
        }
        else
        {
            // UpdateAnim maintains the current playback position (e.g. switching Run_Left to Run_Right)
            _targetAnimator.UpdateAnim(finalName);
        }
    }

    private StringName BuildFinalName()
    {
        if (string.IsNullOrEmpty(_currentDirectionLabel) || DirectionSet == null)
        {
            return _baseAnimName;
        }

        return new StringName($"{_baseAnimName}{DirectionSuffixSeparator}{_currentDirectionLabel}");
    }

    // --- IAnimComponent Pass-through ---
    public void StopAnim() => _targetAnimator.StopAnim();
    public void PauseAnim() => _targetAnimator.PauseAnim();
    public void UpdateAnim(StringName name) => StartAnim(name);
    public bool IsPlaying() => _targetAnimator.IsPlaying();
    public bool HasAnimation(StringName name) => _targetAnimator.HasAnimation(name);
    public void SeekPos(float time, bool updateNow = true) => _targetAnimator.SeekPos(time, updateNow);
    public StringName GetCurrAnimation() => _targetAnimator.GetCurrAnimation();
    public float GetCurrAnimationLength() => _targetAnimator.GetCurrAnimationLength();
    public float GetCurrAnimationPosition() => _targetAnimator.GetCurrAnimationPosition();
    public float GetSpeedScale() => _targetAnimator.GetSpeedScale();
    public void SetSpeedScale(float speedScale) => _targetAnimator.SetSpeedScale(speedScale);
    public Node GetUnderlyingNode() => this;
}
