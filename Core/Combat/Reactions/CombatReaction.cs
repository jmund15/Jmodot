namespace Jmodot.Core.Combat.Reactions;

using System.Collections.Generic;
using Implementation.Combat.Status;
using Stats;

/// <summary>
/// Base class for anything that happened in combat.
/// Listeners check the type (is DamageResult) to know what data to read.
/// </summary>
public abstract record CombatResult
{
    public Node? Source { get; init; }
    public Node? Target { get; init; }
    public IEnumerable<CombatTag> Tags { get; init; }
}

public record DamageResult : CombatResult
{
    public float FinalAmount { get; init; } // Post-armor
    public float OriginalAmount { get; init; } // Pre-armor
    public Vector3 Direction { get; init; }
    public float Force { get; init; }
    public bool IsCritical { get; init; }
    public bool IsFatal { get; init; } // Did this kill them?
}

public record StatResult : CombatResult
{
    public Attribute? AttributeAffected { get; init; }
    // TODO: fill as needed
}

public record HealResult : CombatResult
{
    public float AmountHealed { get; init; }
    public float Overhealing { get; init; }
}

public record StatusResult : CombatResult
{
    public required StatusRunner Runner { get; init; }
}

public record StatusExpiredResult : CombatResult
{
    public required bool WasDispelled { get; init; }
}

// A generic result for things like "Knockback" or "Stun" that might just rely on Tags
public record EffectResult : CombatResult
{
    public string EffectDescription { get; init; } // Debug/Log info
}
