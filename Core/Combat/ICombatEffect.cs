namespace Jmodot.Core.Combat;

using System;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Base class for all data-driven combate logic (Damage, Stun, Knockback).
/// </summary>
/// <remarks>
/// All inheritors should be structs (value type) for memory efficiency and to prevent reference sharing data issues.
/// </remarks>
public interface ICombatEffect
{
    void Apply(ICombatant target, HitContext context);
    void Cancel();

    /// <summary>
    /// Tags associated with this effect (e.g., "Fire", "HeavyHit").
    /// Used for reaction prioritization and state machine integration.
    /// </summary>
    IEnumerable<GameplayTag> Tags { get; }

    /// <summary>
    /// Event triggered when an effect is completed.
    /// </summary>
    /// <remarks>
    /// The event provides two parameters:
    /// 1. The specific <see cref="ICombatEffect"/> instance that completed.
    /// 2. A boolean indicating the final state of the effect (true for fully ran, false for cancelled externally).
    /// This event should be invoked at the end of an effect's lifecycle to notify listeners of its completion.
    /// </remarks>
    event Action<ICombatEffect, bool> EffectCompleted;
}
