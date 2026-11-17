namespace Jmodot.Implementation.Visual.WorldUi;

using System.Collections.Generic;
using System.Linq;
using Godot;
using GCol = Godot.Collections;

/// <summary>
/// Runs its <see cref="Children"/> together, so axes compose (a fade with a slide) without a class per
/// combination. Each child keeps its own timing; this transition's own <c>Timing</c> is unused.
/// </summary>
[GlobalClass, Tool]
public partial class WorldUiCompositeTransition : WorldUiTransition
{
    /// <summary>The transitions to run in parallel.</summary>
    [Export]
    public GCol.Array<WorldUiTransition> Children { get; set; } = new();

    public override float EffectiveDuration => Present().Select(child => child.EffectiveDuration).DefaultIfEmpty(0f).Max();

    public override IEnumerable<string> Faults() => Present().SelectMany(child => child.Faults());

    public override void AddShowTweeners(Tween tween, WorldUiPrompt prompt)
    {
        foreach (WorldUiTransition child in Present())
        {
            child.AddShowTweeners(tween, prompt);
        }
    }

    public override void AddHideTweeners(Tween tween, WorldUiPrompt prompt)
    {
        foreach (WorldUiTransition child in Present())
        {
            child.AddHideTweeners(tween, prompt);
        }
    }

    public override void _ValidateProperty(GCol.Dictionary property)
    {
        if (property["name"].AsStringName() == WorldUiTransition.PropertyName.Timing)
        {
            property["usage"] = (int)PropertyUsageFlags.None;
        }
    }

    private IEnumerable<WorldUiTransition> Present() => Children.Where(child => child != null);
}
