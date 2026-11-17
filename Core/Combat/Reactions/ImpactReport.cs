namespace Jmodot.Core.Combat.Reactions;

using System.Collections.Generic;
using Health;
using Identification;
using Physics;

/// <summary>
/// Base of the records combat publishes once per event so nothing downstream recomputes hit
/// intensity: <see cref="HitReport"/> and the other impact records share the members a sink
/// reads without a type switch. Generic "any result" readers skip every <see cref="ImpactReport"/>,
/// because a report summarizes results the log already holds.
/// </summary>
public abstract record ImpactReport : CombatResult
{
    /// <summary>World position the event happened at.</summary>
    public Vector3 Point { get; init; }

    /// <summary>
    /// Absolute size of the event in [0, 1], independent of the victim: the same spell reads the
    /// same on a mouse and on a boss. Contrast <see cref="HitReport.Weight"/>, which is relative.
    /// </summary>
    public float Magnitude { get; init; }

    /// <summary>The victim's categories (the spell's, for a surface impact), so a reader filters by kind of thing struck.</summary>
    public IReadOnlyList<Category> Categories { get; init; } = [];

    /// <summary>
    /// The damage part of <see cref="Magnitude"/>: for a hit, requested damage over the reference damage; for a
    /// death, the paired hit's value or the profile's default death magnitude. Zero for an event that dealt no
    /// damage, so a sink reads harm without the push.
    /// </summary>
    public float HarmMagnitude { get; init; }
}

/// <summary>
/// The end of an entity with health, however it happened. <see cref="CombatResult.Target"/> is the dead
/// entity. A death that a recent hit explains is <see cref="Paired"/> with it and reads its direction and
/// magnitude; any other reads the profile's default magnitude and has no direction.
/// </summary>
public record DeathReport : ImpactReport
{
    /// <summary>What dealt the blow; null for a removal by rule or when the dead entity did it to itself.</summary>
    public Node? Killer { get; init; }

    /// <summary>Unit vector of the killing blow when <see cref="Paired"/>; zero otherwise.</summary>
    public Vector3 Direction { get; init; }

    /// <summary>Requested damage beyond the health that remained, when a paired hit knew it; zero otherwise.</summary>
    public float Overkill { get; init; }

    /// <summary>The cause of the final health change; <see cref="DamageKind.Removal"/> for a kill by rule.</summary>
    public DamageKind Kind { get; init; }

    /// <summary>True when a hit within the profile's window explains this death.</summary>
    public bool Paired { get; init; }
}

/// <summary>
/// A spell striking something without health: a wall, the floor, a prop. There is no victim combatant to carry
/// it, so it reaches the scene sink only. <see cref="CombatResult.Source"/> is the spell and
/// <see cref="CombatResult.Target"/> is null; <see cref="ImpactReport.Categories"/> are the spell's.
/// </summary>
public record SurfaceImpactReport : ImpactReport
{
    /// <summary>Surface normal at the contact.</summary>
    public Vector3 Normal { get; init; }

    /// <summary>What kind of surface was struck, as the project's collision resolver classifies it.</summary>
    public CollisionReason Surface { get; init; }
}

/// <summary>
/// A direct hit that changed something with health. <see cref="CombatResult.Target"/> is the victim's
/// owner node and <see cref="CombatResult.Source"/> the hitting body or spell.
/// </summary>
public record HitReport : ImpactReport
{
    /// <summary>The root owner of the attack; null when it is the victim itself (a reaction effect).</summary>
    public Node? Attacker { get; init; }

    /// <summary>Unit vector the blow travelled; zero when none is known.</summary>
    public Vector3 Direction { get; init; }

    /// <summary>Health the victim lost.</summary>
    public float Damage { get; init; }

    public bool Killed { get; init; }

    /// <summary>Requested damage beyond the health that remained; zero unless <see cref="Killed"/>.</summary>
    public float Overkill { get; init; }

    /// <summary>Velocity change in m/s the victim's knockback receiver actually applied; zero with none.</summary>
    public float AppliedDeltaV { get; init; }

    public bool IsCritical { get; init; }

    /// <summary>How big the hit was for this victim in [0, 1]: the largest of the damage, push and tag-floor axes.</summary>
    public float Weight { get; init; }

    /// <summary>Damage over the victim's maximum health, in [0, 1].</summary>
    public float DamageFraction { get; init; }

    /// <summary>Applied delta-v over the reference delta-v, in [0, 1].</summary>
    public float PushFraction { get; init; }

    /// <summary><see cref="DamageKind.Direct"/> for payloads and health changes, <see cref="DamageKind.Environmental"/> for slams.</summary>
    public DamageKind Kind { get; init; }

    /// <summary>
    /// The damage scale the hit arrived with, 1 when nothing scaled it. The victim's health applied it before
    /// the report existed, so without it here a reader cannot tell a weak hit on a tough victim from a full
    /// one — the number a floater shows would be the raw damage either way.
    /// </summary>
    public float EffectivenessScale { get; init; } = 1f;
}
