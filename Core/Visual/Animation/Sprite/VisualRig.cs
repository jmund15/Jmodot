namespace Jmodot.Core.Visual.Animation.Sprite;

using Godot;
using GCol = Godot.Collections;

/// <summary>
/// Prefab-level visual contract. Lists every visual node a slot should track, with
/// per-part identity, tags, and override eligibility. Authored on the prefab side
/// (referenced by <c>VisualItemData.Rig</c>) so a slot doesn't have to guess which
/// nodes are visual parts.
/// </summary>
/// <remarks>
/// Without a rig, a slot falls back to a recursive sprite walk and produces tagless,
/// partless handles. Rigs are the way to opt into typed querying (by part, by tag).
/// </remarks>
[GlobalClass]
public partial class VisualRig : Resource
{
    [Export] public GCol.Array<VisualPartBinding> Bindings { get; set; } = new();
}
