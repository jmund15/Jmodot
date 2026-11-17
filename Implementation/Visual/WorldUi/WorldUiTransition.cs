namespace Jmodot.Implementation.Visual.WorldUi;

using System.Collections.Generic;
using Godot;
using Jmodot.Implementation.Visual.Animation;

/// <summary>
/// How a <see cref="WorldUiPrompt"/> shows and hides. A transition is a strategy: a new <c>.tres</c> adds a
/// variant with no change to the prompt. Each rung moves one presentation channel of the prompt; the shared
/// <see cref="Timing"/> sets how fast. A duration of 0, or an unusable one, means no animation.
/// </summary>
[GlobalClass, Tool]
public abstract partial class WorldUiTransition : Resource
{
    /// <summary>Duration (seconds), curve and ease of this transition's motion. Null or an unusable duration snaps.</summary>
    [Export]
    public TweenConfig? Timing { get; set; }

    /// <summary>
    /// The seconds the motion takes: <see cref="Timing"/>'s duration, or 0 when it is null, negative or not finite.
    /// A prompt whose transition reports 0 applies the end state at once.
    /// </summary>
    public virtual float EffectiveDuration => Timing is { } timing && float.IsFinite(timing.Duration) && timing.Duration > 0f ? timing.Duration : 0f;

    /// <summary>One explanation per problem that makes this transition snap, for the owning prompt's configuration warnings.</summary>
    public virtual IEnumerable<string> Faults()
    {
        if (Timing == null)
        {
            yield return $"{GetType().Name} has no Timing, so it snaps instead of animating.";
        }
        else if (!float.IsFinite(Timing.Duration) || Timing.Duration < 0f)
        {
            yield return $"{GetType().Name} Timing has Duration {Timing.Duration}; a negative or non-finite duration snaps instead of animating.";
        }
    }

    /// <summary>
    /// Adds tweeners that move the prompt's presentation channels, from wherever they are now, to the shown rest
    /// state. The prompt hands in a tween whose tweeners run in parallel.
    /// </summary>
    public abstract void AddShowTweeners(Tween tween, WorldUiPrompt prompt);

    /// <summary>
    /// Adds tweeners that move the prompt's presentation channels, from wherever they are now, to the hidden state.
    /// The prompt hides itself when the tween finishes.
    /// </summary>
    public abstract void AddHideTweeners(Tween tween, WorldUiPrompt prompt);

    /// <summary>Adds one tweener for <paramref name="property"/> with this transition's duration, curve and ease.</summary>
    protected void Move(Tween tween, WorldUiPrompt prompt, string property, Variant end)
    {
        PropertyTweener tweener = tween.TweenProperty(prompt, property, end, EffectiveDuration);
        if (Timing != null)
        {
            tweener.SetTrans(Timing.Transition).SetEase(Timing.Ease);
        }
    }
}
