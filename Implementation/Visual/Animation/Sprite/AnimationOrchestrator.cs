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

    [Export]
    public Dictionary<Vector3, string> DirectionLabels { get; set; } = new()
    {
        { Vector3.Forward, "up" },
        { Vector3.Back, "down" },
        { Vector3.Right, "right" },
        { Vector3.Left, "left" },
        { new Vector3(1, 0, -1).Normalized(), "upRight" },
        { new Vector3(-1, 0, -1).Normalized(), "upLeft" },
        { new Vector3(1, 0, 1).Normalized(), "downRight" },
        { new Vector3(-1, 0, 1).Normalized(), "downLeft" }
    };

    private IAnimComponent _targetAnimator = null!;
    private StringName _baseAnimName = "idle";
    private string _currentDirectionLabel = "down";
    public Vector3 CurrentAnimationDirection { get; private set; }

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
        if (direction.IsZeroApprox())
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
            CurrentAnimationDirection = closestDir;
            //GD.Print($"Direction changed from '{_currentDirectionLabel}' to '{newLabel}'");
            _currentDirectionLabel = newLabel;

            // HACK: if not playing just update the anim and position, then pause.
            //  this is a bit jank but possibly ok.
            bool pauseAfter = !IsPlaying();
            UpdateAnim(_baseAnimName, AnimUpdateMode.MaintainTime);
            if (pauseAfter)
            {
                //SeekPos(GetCurrAnimationPosition(), true);
                //PauseAnim();
            }
        }
    }

    /// <summary>
    /// Sets the base state (e.g., "run", "attack"). Triggers a hard reset of the animation.
    /// </summary>
    /// <summary>
    /// Sets the base state (e.g., "run", "attack"). Triggers a hard reset of the animation.
    /// </summary>
    public void StartAnim(StringName baseName)
    {
        UpdateAnim(baseName, AnimUpdateMode.Reset);
    }

    public void UpdateAnim(StringName baseName, AnimUpdateMode mode = AnimUpdateMode.MaintainTime)
    {
        _baseAnimName = baseName;
        var finalName = BuildFinalName();

        //GD.Print($"Anim Orch playing anim '{finalName}' with mode {mode}");

        if (mode == AnimUpdateMode.Reset || !IsPlaying())
        {
            if (_targetAnimator.HasAnimation(finalName))
            {
                _targetAnimator.StartAnim(finalName);
            }
            else if (_targetAnimator.HasAnimation(_baseAnimName))
            {
                _targetAnimator.StartAnim(_baseAnimName);
                JmoLogger.Info(this, $"Animation '{_baseAnimName}' started due to final name not existing '{finalName}'");
            }
            else {
                GD.Print($"Animation '{finalName}' not found on target animator '{_targetAnimator.GetUnderlyingNode().Name}; owner '{_targetAnimator.GetUnderlyingNode().Owner.Name}'");
            }
            // Else: Silently fail or log warning? For now, silent to avoid spam if "idle" is missing.
        }
        else
        {
            // Delegate the update logic (MaintainTime / MaintainPercent) to the child animator
            _targetAnimator.UpdateAnim(finalName, mode);
        }
    }

    public bool HasAnimationBase(StringName baseName)
    {
        var checkName = CheckFinalName(baseName);
        return HasAnimation(checkName);
    }

    private StringName CheckFinalName(StringName baseName)
    {
        if (string.IsNullOrEmpty(_currentDirectionLabel) || DirectionSet == null)
        {
            return baseName;
        }
        return new StringName($"{baseName}{DirectionSuffixSeparator}{_currentDirectionLabel}");
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

    // UpdateAnim is now implemented above
    // public void UpdateAnim(StringName name) => StartAnim(name);
    public bool IsPlaying() => _targetAnimator.IsPlaying();
    public bool HasAnimation(StringName name) => _targetAnimator.HasAnimation(name);
    public void SeekPos(float time, bool updateNow = true) => _targetAnimator.SeekPos(time, updateNow);
    public StringName GetCurrAnimation() => _targetAnimator.GetCurrAnimation();
    public float GetCurrAnimationLength() => _targetAnimator.GetCurrAnimationLength();
    public float GetCurrAnimationPosition() => _targetAnimator.GetCurrAnimationPosition();
    public float GetSpeedScale() => _targetAnimator.GetSpeedScale();
    public void SetSpeedScale(float speedScale) => _targetAnimator.SetSpeedScale(speedScale);
    public string[] GetAnimationList() => _targetAnimator.GetAnimationList();
    public Node GetUnderlyingNode() => this;
}
