// IAttackContracts.cs
using Godot;
using Godot.Collections;

namespace Jmodot.Core.Combat;


/// <summary>
/// A generic data packet representing a single effect to be applied.
/// </summary>
public class CombatEffect
{
    public CombatEffectType Type { get; }
    public Variant Value { get; }

    public CombatEffect(CombatEffectType type, Variant value)
    {
        Type = type;
        Value = value;
    }
}
