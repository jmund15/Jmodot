namespace Jmodot.Implementation.Interaction;

/// <summary>
/// Configuration resource for the grab target selection algorithm.
/// Controls how much the player's facing direction and grabbable type
/// influence target selection scores. Lower score = preferred target.
///
/// Set DirectionInfluence=0 to disable directional bias (pure distance).
/// Set NonReleasableTypePenalty=1.0 to disable type preference.
/// </summary>
[GlobalClass, Tool]
public partial class GrabTargetingConfig : Resource
{
    /// <summary>
    /// How much the player's facing direction influences target selection.
    /// 0.0 = pure distance. 0.5 = moderate directional bias. 1.0 = very strong.
    /// At 0.5: a target directly ahead is scored as ~50% closer.
    /// </summary>
    [Export(PropertyHint.Range, "0,1,0.05")]
    public float DirectionInfluence { get; set; } = 0.5f;

    /// <summary>
    /// Score multiplier for non-releasable grabbables (e.g., potions).
    /// 1.0 = no preference. 1.5 = non-releasable scored as 50% farther.
    /// </summary>
    [Export(PropertyHint.Range, "1,3,0.1")]
    public float NonReleasableTypePenalty { get; set; } = 1.5f;
}
