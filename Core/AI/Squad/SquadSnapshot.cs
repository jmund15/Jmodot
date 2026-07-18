namespace Jmodot.Core.AI.Squad;

using Godot;

/// <summary>
/// Immutable, per-evaluation aggregate of squad state handed to each <see cref="SquadPolicy"/>.
/// Additive-only: consumers construct via named arguments so future field additions stay non-breaking.
/// </summary>
public readonly record struct SquadSnapshot(
    int MemberCount,
    int PeakMemberCount,
    float AverageHealthFraction,
    Node3D? Leader,
    SquadDirectiveDefinition? CurrentDirective,
    float TimeSinceDirectiveChangeSeconds);
