// IAttackContracts.cs
using Godot;
using Godot.Collections;

namespace Jmodot.Core.Combat;

/// <summary>
/// Base class for all data-driven combate logic (Damage, Stun, Knockback).
/// </summary>
[GlobalClass]
public abstract partial class CombatEffect : Resource
{
    public abstract void Apply(ICombatant target, HitContext context);
}
