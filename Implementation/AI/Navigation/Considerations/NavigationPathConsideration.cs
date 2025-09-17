namespace Jmodot.Implementation.AI.Navigation.Considerations;

using System;
using System.Collections.Generic;
using Core.AI.BB;
using Core.AI.Navigation.Considerations;
using Core.Movement;

/// <summary>
///     The path that defines any weight or modifiers for the base navigation path to the target.
///     This is typically optional, but if you want certain entities to regard the path strictly or less of a priority
///     based on certain conditions, use this.
/// </summary>
[GlobalClass]
public partial class NavigationPathConsideration : BaseAIConsideration3D
{
    [Export(PropertyHint.Range, "0,10,0.1,or_greater")]
    private float _baseWeight = 1f;

    /// <summary>
    ///     Child classes must implement this to provide the raw directional scores
    ///     before any personality-driven modifications are applied.
    /// </summary>
    protected override Dictionary<Vector3, float> CalculateBaseScores(DirectionSet3D directions,
        DecisionContext context, IBlackboard blackboard)
    {
        throw new NotImplementedException();
    }
}
