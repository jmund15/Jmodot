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

    // Per-child AnimStarted/AnimFinished handler tracking. Every registered animator
    // (master and slaves) is hooked so slave-only animations (e.g., "potionAdd" on an
    // independent slot) propagate their Started/Finished signals up to the composite.
    // The per-handler closure captures the source animator so the forwarder can apply
    // master-authority suppression: if the master has the animation, non-master
    // signals are dropped to prevent double-fire on partial-match animations.
    private readonly Dictionary<IAnimComponent, Action<StringName>> _childStartHandlers = new();
    private readonly Dictionary<IAnimComponent, Action<StringName>> _childFinishHandlers = new();

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
        if (Engine.IsEditorHint()) { return; }
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
                if (ReferenceEquals(child, this)) { continue; }

                // Don't re-register the master we just added
                if (_activeAnimators.Contains(child)) { continue; }

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
        //GD.Print($"Registering {((Node)animator).Name} to {this.Name}.\tIsMaster = {isMaster}");
        // Prevent self-registration which causes infinite recursion loops
        if (ReferenceEquals(animator, this))
        {
            return;
        }

        if (_activeAnimators.Contains(animator))
        {
            // Already hooked — if re-registering as master, just swap the pointer.
            // Per-child hooks are independent of master status.
            if (isMaster)
            {
                _masterAnimator = animator;
            }
            return;
        }

        _activeAnimators.Add(animator);
        HookChildEvents(animator);

        if (isMaster)
        {
            // Warn when a second animator claims master. New master replaces the old;
            // per-child event hooks are independent of master status, so no unhooking.
            // Usually a config bug (two slots with IsTimeSource=true).
            if (_masterAnimator != null && !ReferenceEquals(_masterAnimator, animator))
            {
                JmoLogger.Warning(this, $"RegisterAnimator: second master claimed by '{((Node)animator).Name}'; replacing existing master '{((Node)_masterAnimator).Name}'. Check for two slots with IsTimeSource=true.");
            }
            _masterAnimator = animator;
        }
        else if (_masterAnimator == null)
        {
            // No master yet — adopt this one as the default.
            _masterAnimator = animator;
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

    /// <summary>
    /// Removes an animator from the composite.
    /// </summary>
    /// <param name="animator">The component to remove.</param>
    /// <param name="stopFirst">
    /// When true (default), <see cref="IAnimComponent.StopAnim"/> is called on the
    /// animator before removal. When false, the caller is responsible for the
    /// animator's playback state — used by <c>VisualSlot.ClearInstance</c> because
    /// the animator's underlying node is about to be <c>QueueFree</c>'d and
    /// touching it with <c>StopAnim</c> is wasteful (and fragile if <c>StopAnim</c>
    /// emits signals synchronously during teardown).
    /// </param>
    public void UnregisterAnimator(IAnimComponent animator, bool stopFirst = true)
    {
        if (!_activeAnimators.Remove(animator)) { return; }

        UnhookChildEvents(animator);

        if (stopFirst) { animator.StopAnim(); }

        // If we lost the master, elect a new one. FirstOrDefault is arbitrary —
        // A2 warned on duplicate masters but didn't add priority. Acceptable since
        // a well-configured scene has exactly one IsTimeSource slot.
        if (ReferenceEquals(_masterAnimator, animator))
        {
            _masterAnimator = _activeAnimators.FirstOrDefault();
            if (_masterAnimator == null)
            {
                JmoLogger.Warning(this, "All animators unregistered; composite has no master. IsPlaying/HasAnimation/etc will return defaults until a new animator registers.");
            }
        }
    }

    private void HookChildEvents(IAnimComponent animator)
    {
        Action<StringName> onStart = name => OnChildAnimStarted(animator, name);
        Action<StringName> onFinish = name => OnChildAnimFinished(animator, name);
        animator.AnimStarted += onStart;
        animator.AnimFinished += onFinish;
        _childStartHandlers[animator] = onStart;
        _childFinishHandlers[animator] = onFinish;
    }

    private void UnhookChildEvents(IAnimComponent animator)
    {
        if (_childStartHandlers.TryGetValue(animator, out var onStart))
        {
            animator.AnimStarted -= onStart;
            _childStartHandlers.Remove(animator);
        }
        if (_childFinishHandlers.TryGetValue(animator, out var onFinish))
        {
            animator.AnimFinished -= onFinish;
            _childFinishHandlers.Remove(animator);
        }
    }

    // Master-authority suppression: if the master has the animation, only the
    // master's signal forwards (prevents double-fire on partial-match where both
    // master and slaves play the same animation). Slave-only animations (master
    // doesn't have them) always forward — the A4 fix for slot-specific animations
    // like "potionAdd" that live on an independent slot's animator.
    private void OnChildAnimStarted(IAnimComponent child, StringName name)
    {
        bool masterHasIt = _masterAnimator?.HasAnimation(name) ?? false;
        if (masterHasIt && !ReferenceEquals(child, _masterAnimator)) { return; }
        AnimStarted?.Invoke(name);
    }

    private void OnChildAnimFinished(IAnimComponent child, StringName name)
    {
        bool masterHasIt = _masterAnimator?.HasAnimation(name) ?? false;
        if (masterHasIt && !ReferenceEquals(child, _masterAnimator)) { return; }
        AnimFinished?.Invoke(name);
    }

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
        if (_masterAnimator == null || child == _masterAnimator) { return; }
        if (!_masterAnimator.IsPlaying()) { return; }

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

    /// <summary>
    /// True iff the MASTER animator has this animation. Strict by design — the master
    /// defines the composite's canonical animation set, so an animation "exists" on
    /// the composite only when the master can drive it.
    /// </summary>
    /// <remarks>
    /// Slave-only animations (e.g., an overlay hat flip) return false here. Use
    /// <see cref="HasAnimationAnywhere"/> if you need "any registered animator has it".
    /// </remarks>
    public bool HasAnimation(StringName animName) =>
        _masterAnimator?.HasAnimation(animName) ?? false;

    /// <summary>
    /// True iff ANY registered animator (master or slave) has this animation.
    /// Use this when you want to know if <see cref="StartAnim"/> would produce
    /// any visible play — since StartAnim uses partial-match semantics.
    /// </summary>
    public bool HasAnimationAnywhere(StringName animName)
    {
        foreach (var anim in _activeAnimators)
        {
            if (anim.HasAnimation(animName)) { return true; }
        }
        return false;
    }

    public bool IsPlaying() => _masterAnimator?.IsPlaying() ?? false;
    public StringName GetCurrAnimation() => _lastRequestedAnim;
    public float GetSpeedScale() => _currentSpeedScale;
    public Node GetUnderlyingNode() => this;

    // Queries delegate to Master
    public float GetCurrAnimationLength() => _masterAnimator?.GetCurrAnimationLength() ?? 0f;
    public float GetAnimationLength(StringName animName) => _masterAnimator?.GetAnimationLength(animName) ?? 0f;
    public float GetCurrAnimationPosition() => _masterAnimator?.GetCurrAnimationPosition() ?? 0f;

    /// <summary>
    /// Seeks the master to <paramref name="time"/> and syncs every slave proportionally.
    /// Interprets <paramref name="time"/> as a position in the MASTER's animation.
    /// A slave whose animation length differs is seeked to the equivalent normalized
    /// position in its own animation — preserving the master/slave time-sync invariant
    /// across hotswap (e.g., sword → lance with different animation durations).
    /// </summary>
    public void SeekPos(float time, bool update = true)
    {
        if (_masterAnimator == null) { return; }

        _masterAnimator.SeekPos(time, update);

        float masterLen = _masterAnimator.GetCurrAnimationLength();
        if (masterLen <= 0f) { return; }
        float norm = time / masterLen;

        foreach (var anim in _activeAnimators)
        {
            if (ReferenceEquals(anim, _masterAnimator)) { continue; }
            float childLen = anim.GetCurrAnimationLength();
            if (childLen > 0f) { anim.SeekPos(norm * childLen, update); }
        }
    }

    public string[] GetAnimationList() => _masterAnimator?.GetAnimationList() ?? [];

    public bool IsAnimationLooping(StringName animName)
    {
        return _masterAnimator?.IsAnimationLooping(animName) ?? false;
    }
}
