namespace Jmodot.Implementation.Visual.WorldUi;

using Godot;

/// <summary>Slides the prompt: its offset runs between <see cref="Offset"/> (hidden) and zero, its laid-out position (shown).</summary>
[GlobalClass, Tool]
public partial class WorldUiSlideTransition : WorldUiTransition
{
    /// <summary>The hidden position relative to the laid-out one, in metres. (0, -0.15, 0) slides up from below.</summary>
    [Export]
    public Vector3 Offset { get; set; } = new(0f, -0.15f, 0f);

    public override void AddShowTweeners(Tween tween, WorldUiPrompt prompt)
        => Move(tween, prompt, nameof(WorldUiPrompt.PresentationOffset), Vector3.Zero);

    public override void AddHideTweeners(Tween tween, WorldUiPrompt prompt)
        => Move(tween, prompt, nameof(WorldUiPrompt.PresentationOffset), Offset);
}
