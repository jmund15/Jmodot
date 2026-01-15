namespace Jmodot.Core.Combat;

/// <summary>
/// Defines what happens when an incoming effect with one Category
/// interacts with an existing effect of another Category.
/// </summary>
public enum CategoryInteractionEffect
{
    /// <summary>Remove all existing effects with the matching Category.</summary>
    CancelExisting = 0,

    /// <summary>Reduce duration of existing effects by Magnitude seconds.</summary>
    ReduceDuration = 1,

    /// <summary>Block the incoming effect from being applied.</summary>
    CancelIncoming = 2,

    /// <summary>Remove both the incoming and existing effects (mutual annihilation).</summary>
    CancelBoth = 3,

    /// <summary>Replace existing effect with TransformEffect.</summary>
    Transform = 4,

    /// <summary>Increase existing effect's intensity/duration by Magnitude.</summary>
    Amplify = 5
}
