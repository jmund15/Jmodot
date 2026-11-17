namespace Jmodot.Implementation.Visual.WorldUi;

using System.Collections.Generic;
using Godot;

/// <summary>Scales the prompt: its scale runs between <see cref="FromScale"/> (hidden) and 1 (shown).</summary>
[GlobalClass, Tool]
public partial class WorldUiScaleTransition : WorldUiTransition
{
    /// <summary>The hidden scale, as a multiple of the shown size. A zero or negative scale collapses or flips the prompt.</summary>
    [Export(PropertyHint.Range, "0.01,1,0.01")]
    public float FromScale { get; set; } = 0.8f;

    public override IEnumerable<string> Faults()
    {
        foreach (string fault in base.Faults())
        {
            yield return fault;
        }

        if (!float.IsFinite(FromScale) || FromScale <= 0f)
        {
            yield return $"{GetType().Name} FromScale is {FromScale}; a non-positive or non-finite hidden scale collapses the prompt.";
        }
    }

    public override void AddShowTweeners(Tween tween, WorldUiPrompt prompt)
        => Move(tween, prompt, nameof(WorldUiPrompt.PresentationScale), 1f);

    public override void AddHideTweeners(Tween tween, WorldUiPrompt prompt)
        => Move(tween, prompt, nameof(WorldUiPrompt.PresentationScale), FromScale);
}
