namespace Jmodot.Implementation.Visual.WorldUi;

using Godot;

/// <summary>Fades the prompt: its alpha runs between 0 (hidden) and 1 (shown).</summary>
[GlobalClass, Tool]
public partial class WorldUiFadeTransition : WorldUiTransition
{
    public override void AddShowTweeners(Tween tween, WorldUiPrompt prompt)
        => Move(tween, prompt, nameof(WorldUiPrompt.PresentationAlpha), 1f);

    public override void AddHideTweeners(Tween tween, WorldUiPrompt prompt)
        => Move(tween, prompt, nameof(WorldUiPrompt.PresentationAlpha), 0f);
}
