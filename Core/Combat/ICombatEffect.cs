namespace Jmodot.Core.Combat;

using System;
using System.Collections;
using System.Collections.Generic;
using Godot;
using Reactions;
using Visual.Effects;

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
    /// <paramref name="incomingMagnitudeScale"/> scales the effect's damage contribution for this
    /// application (per-application operand; <c>1.0f</c> neutral). A NON-POSITIVE operand means the
    /// damage contribution is fully suppressed — implementations must report that suppression on an
    /// observable channel rather than letting the application silently do nothing.
    /// </summary>
    CombatResult? Apply(ICombatant target, HitContext context, float incomingMagnitudeScale = 1.0f);

    /// <summary>
    /// Tags associated with this effect (e.g., "Fire", "HeavyHit").
    /// Used for reaction prioritization and state machine integration.
    /// </summary>
    IEnumerable<CombatTag> Tags { get; }
    
    /// <summary>
    /// Optional visual effect (tint, flash) to apply to the target.
    /// </summary>
    VisualEffect? Visual { get; }
}
