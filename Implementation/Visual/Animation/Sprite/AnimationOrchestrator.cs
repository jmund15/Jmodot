namespace Jmodot.Implementation.Visual.Animation.Sprite;

using System;
using Core.Movement;
using Core.Visual.Animation.Sprite;
using Godot;
using Godot.Collections;
using Shared;
using Jmodot.Core.Shared.Attributes;

/// <summary>
/// Coordinates the high-level animation state (e.g. "run_left").
/// Combines a Base Name (State) with a Direction Suffix.
/// </summary>
[GlobalClass, Tool]
public partial class AnimationOrchestrator : Node, IAnimationOrchestrator
{
    [Export, RequiredExport] private Node _targetAnimatorNode = null!;
    [Export] public string DirectionSuffixSeparator { get; set; } = "_";

    // Helper resource to map Vector3 -> "left", "down_right", etc.
    // If null, direction logic is skipped.
    [Export] public DirectionSet3D? DirectionSet { get; set; }

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
    public StringName BaseAnimName { get; private set; } = "idle";
    public string CurrentDirectionLabel { get; private set; } = "down";
    public Vector3 CurrentAnimationDirection { get; private set; }

    // Rate-limit: warn once per missing animation key per orchestrator instance.
    // Without this, a stuck state polling for the missing clip would spam logs every physics tick.
    // Fully qualified to avoid colliding with Godot.Collections in the [Export] Dictionary above.
    private readonly System.Collections.Generic.HashSet<StringName> _warnedMissingAnims = new();

    public event Action<StringName> AnimStarted = delegate { };
    public event Action<StringName> AnimFinished = delegate { };
    public event Action<StringName> AnimStopped = delegate { };


    public override void _Ready()
    {
        if (Engine.IsEditorHint())
        {
            return;
        }
        this.ValidateRequiredExports();
        _targetAnimator = _targetAnimatorNode as IAnimComponent;
        if (_targetAnimator == null)
        {
            JmoLogger.Error(this, $"Orchestrator '{Name}': Target is not an IAnimComponent.");
            SetProcess(false);
            return;
        }

        // Forward events
        _targetAnimator.AnimStarted += OnTargetAnimStarted;
        _targetAnimator.AnimFinished += OnTargetAnimFinished;
        _targetAnimator.AnimStopped += OnTargetAnimStopped;
    }

    public override void _EnterTree()
    {
        base._EnterTree();
        if (Engine.IsEditorHint()) { return; }

        // Re-subscribe after reparent (e.g., DetachFromParent in spell destroy flow).
        // _Ready resolves _targetAnimator once; _ExitTree unsubscribes.
        // On first entry _targetAnimator is null (not yet resolved), so this is a no-op.
        if (_targetAnimator != null)
        {
            _targetAnimator.AnimStarted += OnTargetAnimStarted;
            _targetAnimator.AnimFinished += OnTargetAnimFinished;
            _targetAnimator.AnimStopped += OnTargetAnimStopped;
        }
    }

    public override void _ExitTree()
    {
        base._ExitTree();
        if (_targetAnimator != null)
        {
            _targetAnimator.AnimStarted -= OnTargetAnimStarted;
            _targetAnimator.AnimFinished -= OnTargetAnimFinished;
            _targetAnimator.AnimStopped -= OnTargetAnimStopped;
        }
    }

    private void OnTargetAnimStarted(StringName n) => AnimStarted.Invoke(n);
    private void OnTargetAnimFinished(StringName n) => AnimFinished.Invoke(n);
    private void OnTargetAnimStopped(StringName n) => AnimStopped.Invoke(n);

    /// <summary>
    /// Updates the direction. Triggers a smooth update to preserve animation time.
    /// </summary>
    public void SetDirection(Vector3 direction)
    {
        if (direction.IsZeroApprox()) { return; }
        if (DirectionSet == null) { return; }

        var closestDir = DirectionSet.GetClosestDirection(direction);
        if (closestDir.IsZeroApprox()) { return; } // DirectionSet not yet initialized or empty
        if (!DirectionLabels.TryGetValue(closestDir, out var newLabel))
        {
            JmoLogger.Error(this, $"Direction Set '{DirectionSet.ResourceName}' does not contain direction '{closestDir}");
            return;
        }

        if (newLabel != CurrentDirectionLabel)
        {
            CurrentAnimationDirection = closestDir;
            //GD.Print($"Direction changed from '{_currentDirectionLabel}' to '{newLabel}'");
            CurrentDirectionLabel = newLabel;

            bool wasNotPlaying = !IsPlaying();
            UpdateAnim(BaseAnimName, AnimUpdateMode.MaintainTime);
            if (wasNotPlaying && IsPlaying())
            {
                // Animation had completed (e.g., charge form fully formed).
                // UpdateAnim restarted it for the new direction — seek to end
                // to preserve the "completed" visual state.
                // IsPlaying() recheck: UpdateAnim no-ops if neither finalName nor
                // BaseAnimName resolves on the target animator, leaving no current
                // clip — GetCurrAnimationLength() would then error.
                SeekPos(GetCurrAnimationLength(), true);
            }
        }
    }

    /// <summary>
    /// Sets the base state (e.g., "run", "attack"). Triggers a hard reset of the animation.
    /// </summary>
    public void StartAnim(StringName baseName)
    {
        UpdateAnim(baseName, AnimUpdateMode.Reset);
    }

    public void UpdateAnim(StringName baseName, AnimUpdateMode mode = AnimUpdateMode.MaintainTime)
    {
        BaseAnimName = baseName;
        var finalName = BuildFinalName();

        var playable = ResolvePlayableAnimation(finalName);
        if (playable == null)
        {
            HandleMissingAnimation(finalName);
            return;
        }

        if (mode == AnimUpdateMode.Reset || !IsPlaying())
        {
            _targetAnimator.StartAnim(playable);
        }
        else
        {
            _targetAnimator.UpdateAnim(playable, mode);
        }
    }

    /// <summary>
    /// Resolves which clip the target animator should play for <paramref name="finalName"/>,
    /// degrading through three tiers: (1) the exact directional clip; (2) the nearest available
    /// directional clip by angular proximity to the current facing — lets a 4-directional art set
    /// serve an 8-direction request (e.g. "downLeft" → "left"); (3) the undirected base clip.
    /// Returns null when no clip exists for this base name at all.
    /// </summary>
    private StringName? ResolvePlayableAnimation(StringName finalName)
    {
        if (_targetAnimator.HasAnimation(finalName))
        {
            return finalName;
        }

        var closestDirectional = FindClosestAvailableDirectional();
        if (closestDirectional != null)
        {
            return closestDirectional;
        }

        if (_targetAnimator.HasAnimation(BaseAnimName))
        {
            return BaseAnimName;
        }

        return null;
    }

    /// <summary>
    /// Among the directional variants of <see cref="BaseAnimName"/> that actually exist on the
    /// target animator, returns the one whose direction is closest (max dot product) to the
    /// current facing. Returns null if direction logic is disabled or no directional clip exists.
    /// Equidistant ties resolve to the first match in DirectionLabels insertion order (strict
    /// greater-than) — deterministic, and the choice between two equidistant directions is cosmetic.
    /// </summary>
    private StringName? FindClosestAvailableDirectional()
    {
        if (DirectionSet == null || CurrentAnimationDirection.IsZeroApprox())
        {
            return null;
        }

        StringName? best = null;
        var bestDot = float.MinValue;
        foreach (var kvp in DirectionLabels)
        {
            var candidate = new StringName($"{BaseAnimName}{DirectionSuffixSeparator}{kvp.Value}");
            if (!_targetAnimator.HasAnimation(candidate))
            {
                continue;
            }

            var dot = kvp.Key.Dot(CurrentAnimationDirection);
            if (dot > bestDot)
            {
                bestDot = dot;
                best = candidate;
            }
        }

        return best;
    }

    /// <summary>
    /// Last-resort handling when no clip resolves for the request. Warns once, then synthesizes a
    /// deferred AnimFinished so a state machine gating its exit on AnimFinished (e.g. CraftSpellState)
    /// degrades to "zero-length clip completed" instead of hanging forever. Deferred — not synchronous —
    /// to match the engine's always-async animation_finished and to fire after a caller that subscribes
    /// to AnimFinished immediately *after* calling StartAnim within the same frame.
    /// </summary>
    private void HandleMissingAnimation(StringName finalName)
    {
        WarnMissingAnimationOnce(finalName, BaseAnimName);
        Callable.From(() => AnimFinished.Invoke(finalName)).CallDeferred();
    }

    private void WarnMissingAnimationOnce(StringName finalName, StringName baseName)
    {
        if (!_warnedMissingAnims.Add(baseName))
        {
            return;
        }

        JmoLogger.Warning(this,
            $"Animation not found on target animator: tried '{finalName}' (directional) and '{baseName}' (base). "
            + "State machine may stall waiting for AnimFinished. Check the animation library or the state's AnimationName export.");
    }

    public bool HasAnimationBase(StringName baseName)
    {
        var checkName = CheckFinalName(baseName);
        return HasAnimation(checkName);
    }

    private StringName CheckFinalName(StringName baseName)
    {
        if (string.IsNullOrEmpty(CurrentDirectionLabel) || DirectionSet == null)
        {
            return baseName;
        }
        return new StringName($"{baseName}{DirectionSuffixSeparator}{CurrentDirectionLabel}");
    }
    private StringName BuildFinalName()
    {
        if (string.IsNullOrEmpty(CurrentDirectionLabel) || DirectionSet == null)
        {
            return BaseAnimName;
        }

        return new StringName($"{BaseAnimName}{DirectionSuffixSeparator}{CurrentDirectionLabel}");
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
    public float GetAnimationLength(StringName animName) => _targetAnimator.GetAnimationLength(animName);
    public float GetAnimationLengthBase(StringName baseName)
    {
        var finalName = CheckFinalName(baseName);
        if (HasAnimation(finalName))
        {
            return GetAnimationLength(finalName);
        }
        return GetAnimationLength(baseName);
    }
    public float GetCurrAnimationPosition() => _targetAnimator.GetCurrAnimationPosition();
    public float GetSpeedScale() => _targetAnimator.GetSpeedScale();
    public void SetSpeedScale(float speedScale) => _targetAnimator.SetSpeedScale(speedScale);
    public string[] GetAnimationList() => _targetAnimator.GetAnimationList();

    public bool IsAnimationLooping(StringName baseName)
    {
        var finalName = CheckFinalName(baseName);
        return _targetAnimator.IsAnimationLooping(finalName);
    }

    public Node GetUnderlyingNode() => this;
}
