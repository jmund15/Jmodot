namespace Jmodot.Core.AI.Navigation.SteeringModifiers;

using System.Collections.Generic;
using BB;
using Implementation.AI.Navigation;

/// <summary>
///     An abstract resource that modifies the directional scores produced by a steering consideration.
///     This allows an AI's personality (Affinities) to influence its low-level movement behavior.
/// </summary>
/// <remarks>
///     <para><b>Modifier-Consideration Compatibility:</b> Any modifier can be attached to any
///     consideration via the Inspector, but not all combinations are meaningful. Modifiers that
///     depend on specific <see cref="SteeringDecisionContext3D"/> fields (e.g., TargetPosition)
///     will silently no-op when those fields are not populated. Each concrete modifier should
///     document which context fields it requires and which considerations it works well with.</para>
///
///     <para><b>Negative Score Interaction:</b> Modifiers multiply ALL scores including negative
///     penalties (e.g., ZoneBoundaryConsideration3D direction penalties). A multiplier &gt; 1
///     amplifies penalties (stronger avoidance); &lt; 1 dampens them (weaker avoidance). This is
///     intentional — a "cautious" AI should have stronger boundary avoidance.</para>
///
///     <para><b>Universal vs Context-Dependent:</b> Some modifiers (AffinitySteeringModifier3D)
///     are universally applicable — they read from the blackboard, not the context struct. Others
///     (DistanceScalingModifier3D) require specific context fields and should document those
///     dependencies via remarks.</para>
/// </remarks>
[GlobalClass]
public abstract partial class SteeringConsiderationModifier3D : Resource
{
    /// <summary>
    ///     Modifies the dictionary of steering scores.
    /// </summary>
    /// <param name="scores">The current dictionary of directional scores to be modified.</param>
    /// <param name="context">The per-frame snapshot of the AI's state and world view.</param>
    /// <param name="blackboard">The AI's blackboard for accessing core components.</param>
    public abstract void Modify(ref Dictionary<Vector3, float> scores, SteeringDecisionContext3D context, IBlackboard blackboard);
}
