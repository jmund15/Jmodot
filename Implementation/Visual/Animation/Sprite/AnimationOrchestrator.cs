namespace Jmodot.Implementation.Visual.Animation.Sprite;

using System;
using Godot;
using Godot.Collections;
using System.Linq;
using Jmodot.Core.Visual.Animation.Sprite;

/// <summary>
/// The central controller for the sprite animation system. It takes a base animation name
/// and combines it with variants from its collection of AnimVariantSource resources
/// to construct and play the final animation on a target IAnimComponent.
/// </summary>
[GlobalClass]
public partial class AnimationOrchestrator : Node, IAnimComponent
{
    [Export] private Node _targetAnimatorNode;
    [Export] public AnimationNamingConvention NamingConvention { get; set; }

    /// <summary>
    /// The collection of AnimVariantSource resources that will contribute to the final animation name.
    /// </summary>
    [Export] public Array<AnimVariantSource> VariantSources { get; set; } = new();

    private IAnimComponent _targetAnimator;
    private StringName _baseAnimName = "";

    public override void _Ready()
    {
        _targetAnimator = _targetAnimatorNode as IAnimComponent;
        if (_targetAnimator == null)
        {
            GD.PrintErr($"AnimationOrchestrator '{Name}': Target animator at '{_targetAnimatorNode}' is not a valid IAnimComponent.");
            SetProcess(false);
        }
    }

    // /// <summary>
    // /// Plays an animation by combining the base name with all registered variants.
    // /// This is the primary method your state machine or controller should call.
    // /// </summary>
    // public void Play(StringName baseAnimName)
    // {
    //     _baseAnimName = baseAnimName;
    //     StringName finalAnimName = BuildFinalAnimationName();
    //     if (_targetAnimator.GetCurrAnimation() != finalAnimName)
    //     {
    //          _targetAnimator.StartAnim(finalAnimName);
    //     }
    // }
    //
    /// <summary>
    /// Updates the currently playing animation if the variants have changed,
    /// while attempting to preserve the animation's progress.
    /// </summary>
    private void Update()
    {
        StringName finalAnimName = BuildFinalAnimationName();
        _targetAnimator.UpdateAnim(finalAnimName);
    }

    /// <summary>
    /// Provides a new direction to all registered DirectionalVariantSource resources.
    /// </summary>
    public void SetDirection(Vector3 direction)
    {
        foreach (var source in VariantSources.OfType<Directional3DVariantSource>())
        {
            source.UpdateDirection(direction);
        }

        Update();
    }

    /// <summary>
    /// Provides a new style to all registered StyleVariantSource resources.
    /// </summary>
    public void SetStyle(StringName style)
    {
        foreach (var source in VariantSources.OfType<StyleVariantSource>())
        {
            source.UpdateStyle(style);
        }

        Update();
    }

    private StringName BuildFinalAnimationName()
    {
        if (NamingConvention == null) { GD.PrintErr($"AnimationOrchestrator '{Name}' has no NamingConvention resource assigned."); return _baseAnimName; }

        var variants = VariantSources
            .OrderBy(s => s.Order)
            .Select(s => s.GetAnimVariant());

        return NamingConvention.GetFullAnimationName(_baseAnimName, variants);
    }

    public Node GetUnderlyingNode()
    {
        return this;
    }

    public event Action<StringName>? AnimStarted
    {
        add => _targetAnimator.AnimStarted += value;
        remove => _targetAnimator.AnimStarted -= value;
    }

    public event Action<StringName>? AnimFinished
    {
        add => _targetAnimator.AnimFinished += value;
        remove => _targetAnimator.AnimFinished -= value;
    }

    public void StartAnim(StringName animName)
    {
        _baseAnimName = animName;
        StringName finalAnimName = BuildFinalAnimationName();
        if (_targetAnimator.GetCurrAnimation() != finalAnimName)
        {
            _targetAnimator.StartAnim(finalAnimName);
        }
    }

    public void PauseAnim()
    {
        _targetAnimator.PauseAnim();
    }

    public void StopAnim()
    {
        _targetAnimator.StopAnim();
    }

    public void UpdateAnim(StringName animName)
    {
        StringName finalAnimName = BuildFinalAnimationName();
        _targetAnimator.UpdateAnim(finalAnimName);
    }

    public bool IsPlaying()
    {
        return _targetAnimator.IsPlaying();
    }

    public bool HasAnimation(StringName animName)
    {
        var fullAnimName = BuildFinalAnimationName();
        return _targetAnimator.HasAnimation(fullAnimName);
    }

    public StringName GetCurrAnimation()
    {
        return _targetAnimator.GetCurrAnimation();
    }

    public float GetSpeedScale()
    {
        return _targetAnimator.GetSpeedScale();
    }

    public void SetSpeedScale(float speedScale)
    {
        _targetAnimator.SetSpeedScale(speedScale);
    }
}
