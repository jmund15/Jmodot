namespace Jmodot.Implementation.Visual.Animation.Sprite;

using Godot;
using System.Collections.Generic;
using System.Linq;
using System;
using Core.Visual.Animation.Sprite;
using Shared;

/// <summary>
/// Acts as a single Animator but broadcasts commands to a dynamic list of children.
/// Handles time synchronization, ensuring "Slave" animators match the "Master" (Body).
/// </summary>
[GlobalClass, Tool]
public partial class CompositeAnimatorComponent : Node, IAnimComponent
{
    /// <summary>
    /// If true, automatically finds and registers all IAnimComponent children on _Ready.
    /// Useful for simple "Plug and Play" setups (e.g. Snowball) without a VisualComposer.
    /// </summary>
    [Export] public bool AutoFindChildren { get; set; } = false;

    /// <summary>
    /// Optional: Manually assign the Master Animator (Time Source) in the editor.
    /// If set, this animator will dictate the duration/seek of the composite.
    /// </summary>
    [Export] public Node? MasterAnimatorNode { get; set; }

    private readonly List<IAnimComponent> _activeAnimators = new();
    private IAnimComponent? _masterAnimator; // The time source (e.g. Body)

    private StringName _lastRequestedAnim = "";
    private float _currentSpeedScale = 1.0f;

    public event Action<StringName> AnimStarted = delegate { };
    public event Action<StringName> AnimFinished = delegate { };
    public override void _PhysicsProcess(double delta)
    {
        base._PhysicsProcess(delta);
    }

    public override void _Ready()
    {
        if (Engine.IsEditorHint()) return;
        // 1. Register Manual Master
        if (MasterAnimatorNode != null)
        {
            if (MasterAnimatorNode is IAnimComponent masterAnim)
            {
                RegisterAnimator(masterAnim, isMaster: true);
            }
            else
            {
                JmoLogger.Error(this, $"Assigned MasterAnimatorNode '{MasterAnimatorNode.Name}' is not an IAnimComponent.");
            }
        }

        // 2. Auto-Discovery
        if (AutoFindChildren)
        {
            // Use NodeExts to find all children implementing IAnimComponent
            var children = this.GetChildrenOfInterface<IAnimComponent>(includeSubChildren: true);
            foreach (var child in children)
            {
                // Don't register self if we somehow found ourselves (unlikely with GetChildren, but safe)
                if (ReferenceEquals(child, this)) continue;

                // Don't re-register the master we just added
                if (_activeAnimators.Contains(child)) continue;

                RegisterAnimator(child, isMaster: false);
            }
        }
    }

    /// <summary>
    /// Adds an animator to the composite.
    /// </summary>
    /// <param name="animator">The component to control.</param>
    /// <param name="isMaster">If true, this animator dictates timing (duration/seek).</param>
    public void RegisterAnimator(IAnimComponent animator, bool isMaster = false)
    {
        GD.Print($"Registering {((Node)animator).Name} to {this.Name}.\nIsMaster = {isMaster}");
        // Prevent self-registration which causes infinite recursion loops
        if (ReferenceEquals(animator, this))
        {
            return;
        }

        if (_activeAnimators.Contains(animator))
        {
            // If re-registering as master, update status
            if (isMaster)
            {
                SetMaster(animator);
            }
            return;
        }

        _activeAnimators.Add(animator);

        if (isMaster || _masterAnimator == null)
        {
            SetMaster(animator);
            //GD.Print($"Set new anim {((Node)animator).Name} as master");
        }

        // Apply current state to new child
        animator.SetSpeedScale(_currentSpeedScale);

        // If playing, catch up immediately
        if (!string.IsNullOrEmpty(_lastRequestedAnim) && animator.HasAnimation(_lastRequestedAnim))
        {
            animator.StartAnim(_lastRequestedAnim);
            SyncChildToMaster(animator);
        }
    }

    public void UnregisterAnimator(IAnimComponent animator)
    {
        if (_activeAnimators.Remove(animator))
        {
            animator.StopAnim();

            // If we lost the master, elect a new one
            if (_masterAnimator == animator)
            {
                UnhookMaster(animator);
                _masterAnimator = _activeAnimators.FirstOrDefault();
                if (_masterAnimator != null) HookMaster(_masterAnimator);
            }
        }
    }

    private void SetMaster(IAnimComponent newMaster)
    {
        if (_masterAnimator != null)
        {
            UnhookMaster(_masterAnimator);
        }
        _masterAnimator = newMaster;
        HookMaster(_masterAnimator);
    }

    private void HookMaster(IAnimComponent anim)
    {
        anim.AnimStarted += OnMasterAnimStarted;
        anim.AnimFinished += OnMasterAnimFinished;
    }

    private void UnhookMaster(IAnimComponent anim)
    {
        anim.AnimStarted -= OnMasterAnimStarted;
        anim.AnimFinished -= OnMasterAnimFinished;
    }

    private void OnMasterAnimStarted(StringName name) => AnimStarted?.Invoke(name);
    private void OnMasterAnimFinished(StringName name) => AnimFinished?.Invoke(name);

    // --- IAnimComponent Implementation ---

    public void StartAnim(StringName animName)
    {
        _lastRequestedAnim = animName;

        foreach (var anim in _activeAnimators)
        {
            // Partial Match Logic: Only play if the component actually has the animation.
            // This allows "Face" to have unique anims that "Body" ignores.
            if (anim.HasAnimation(animName))
            {
                anim.StartAnim(animName);
            }
            // else
            // {
            //     anim.StopAnim();
            // }
        }
    }

    public void UpdateAnim(StringName animName, AnimUpdateMode mode = AnimUpdateMode.MaintainTime)
    {
        _lastRequestedAnim = animName;

        foreach (var anim in _activeAnimators)
        {
            if (anim.HasAnimation(animName))
            {
                anim.UpdateAnim(animName, mode);
            }
        }
    }

    private void SyncChildToMaster(IAnimComponent child)
    {
        if (_masterAnimator == null || child == _masterAnimator) return;
        if (!_masterAnimator.IsPlaying()) return;

        float norm = GetMasterNormalizedTime();
        float childLen = child.GetCurrAnimationLength();

        if (childLen > 0)
        {
            child.SeekPos(norm * childLen);
        }
    }

    private float GetMasterNormalizedTime()
    {
        if (_masterAnimator != null && _masterAnimator.GetCurrAnimationLength() > 0)
        {
            return _masterAnimator.GetCurrAnimationPosition() / _masterAnimator.GetCurrAnimationLength();
        }
        return 0f;
    }

    public void StopAnim() => _activeAnimators.ForEach(a => a.StopAnim());
    public void PauseAnim() => _activeAnimators.ForEach(a => a.PauseAnim());

    public void SetSpeedScale(float scale)
    {
        _currentSpeedScale = scale;
        _activeAnimators.ForEach(a => a.SetSpeedScale(scale));
    }

    public bool HasAnimation(StringName animName) =>
        _masterAnimator?.HasAnimation(animName) ?? false; // master is REQUIRED to say yes, others not needed but should play if available
        //_activeAnimators.Any(a => a.HasAnimation(animName));
    public bool IsPlaying() => _masterAnimator?.IsPlaying() ?? false;
    public StringName GetCurrAnimation() => _lastRequestedAnim;
    public float GetSpeedScale() => _currentSpeedScale;
    public Node GetUnderlyingNode() => this;

    // Queries delegate to Master
    public float GetCurrAnimationLength() => _masterAnimator?.GetCurrAnimationLength() ?? 0f;
    public float GetAnimationLength(StringName animName) => _masterAnimator?.GetAnimationLength(animName) ?? 0f;
    public float GetCurrAnimationPosition() => _masterAnimator?.GetCurrAnimationPosition() ?? 0f;
    public void SeekPos(float time, bool update = true) => _activeAnimators.ForEach(a => a.SeekPos(time, update));
    public string[] GetAnimationList() => _masterAnimator?.GetAnimationList() ?? [];
}
