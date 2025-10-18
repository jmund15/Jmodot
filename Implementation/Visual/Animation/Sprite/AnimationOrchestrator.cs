namespace Jmodot.Implementation.Visual.Animation.Sprite;

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
public partial class AnimationOrchestrator : Node
{
    [Export] private NodePath _targetAnimatorPath;
    [Export] public AnimationNamingConvention NamingConvention { get; set; }

    /// <summary>
    /// The collection of AnimVariantSource resources that will contribute to the final animation name.
    /// </summary>
    [Export] public Array<AnimVariantSource> VariantSources { get; set; } = new();

    private IAnimComponent _targetAnimator;
    private string _baseAnimName = "";

    public override void _Ready()
    {
        _targetAnimator = GetNode<Node>(_targetAnimatorPath) as IAnimComponent;
        if (_targetAnimator == null)
        {
            GD.PrintErr($"AnimationOrchestrator '{Name}': Target animator at '{_targetAnimatorPath}' is not a valid IAnimComponent.");
            SetProcess(false);
        }
    }

    /// <summary>
    /// Plays an animation by combining the base name with all registered variants.
    /// This is the primary method your state machine or controller should call.
    /// </summary>
    public void Play(string baseAnimName)
    {
        _baseAnimName = baseAnimName;
        string finalAnimName = BuildFinalAnimationName();
        if (_targetAnimator.GetCurrAnimation() != finalAnimName)
        {
             _targetAnimator.StartAnim(finalAnimName);
        }
    }

    /// <summary>
    /// Updates the currently playing animation if the variants have changed,
    /// while attempting to preserve the animation's progress.
    /// </summary>
    public void Update()
    {
        string finalAnimName = BuildFinalAnimationName();
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
    }

    /// <summary>
    /// Provides a new style to all registered StyleVariantSource resources.
    /// </summary>
    public void SetStyle(string style)
    {
        foreach (var source in VariantSources.OfType<StyleVariantSource>())
        {
            source.UpdateStyle(style);
        }
    }

    private string BuildFinalAnimationName()
    {
        if (NamingConvention == null) { GD.PrintErr($"AnimationOrchestrator '{Name}' has no NamingConvention resource assigned."); return _baseAnimName; }

        var variants = VariantSources
            .OrderBy(s => s.Order)
            .Select(s => s.Getiant());

        return NamingConvention.GetFullAnimationName(_baseAnimName, variants);
    }
}
