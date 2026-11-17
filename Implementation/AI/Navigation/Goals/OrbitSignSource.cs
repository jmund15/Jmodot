namespace Jmodot.Implementation.AI.Navigation.Goals;

/// <summary>Which end of the ring an orbit follows.</summary>
public enum OrbitSignSource
{
    /// <summary>One direction, reversing at the authored interval.</summary>
    SeededFlip = 0,

    /// <summary>Follows the target's sideways velocity; holds ground while the target stands.</summary>
    MatchTargetLateral = 1,
}
