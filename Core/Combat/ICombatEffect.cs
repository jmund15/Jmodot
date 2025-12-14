namespace Jmodot.Core.Combat;

using System;
using System.Collections;
using System.Collections.Generic;
using Reactions;

/// <summary>
/// Base class for all data-driven combate logic (Damage, Stun, Knockback).
/// </summary>
/// <remarks>
/// All inheritors should be structs (value type) for memory efficiency and to prevent reference sharing data issues.
/// </remarks>
public interface ICombatEffect
{
    /// <summary>
    /// Applies the logic to the target and returns a snapshot of what happened.
    /// Returns null if the effect failed or did nothing.
    /// </summary>
    CombatResult? Apply(ICombatant target, HitContext context);

    /// <summary>
    /// Tags associated with this effect (e.g., "Fire", "HeavyHit").
    /// Used for reaction prioritization and state machine integration.
    /// </summary>
    IEnumerable<CombatTag> Tags { get; }
}
