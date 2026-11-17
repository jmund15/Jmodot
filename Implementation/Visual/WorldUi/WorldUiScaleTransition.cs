namespace Jmodot.Implementation.Visual.WorldUi;

using Godot;

/// <summary>Scales the prompt: its scale runs between <see cref="FromScale"/> (hidden) and 1 (shown).</summary>
[GlobalClass, Tool]
public partial class WorldUiScaleTransition : WorldUiTransition
{
    /// <summary>The hidden scale, as a multiple of the shown size.</summary>
    [Export]
    public float FromScale { get; set; } = 0.8f;

    public override void AddShowTweeners(Tween tween, WorldUiPrompt prompt)
        => Move(tween, prompt, nameof(WorldUiPrompt.PresentationScale), 1f);

    public override void AddHideTweeners(Tween tween, WorldUiPrompt prompt)
        => Move(tween, prompt, nameof(WorldUiPrompt.PresentationScale), FromScale);
}
